using EnergyAutomate;
using EnergyAutomate.Data;
using EnergyAutomate.Definitions;
using EnergyAutomate.Watchdogs;
using Growatt.OSS;
using Growatt.Sdk;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Linq;
using Tibber.Sdk;
using static Microsoft.AspNetCore.Components.WebAssembly.HotReload.WebAssemblyHotReload;

public partial class ApiService : IObserver<RealTimeMeasurement>, IDisposable
{
    #region Fields

    private int timerPenalty = 0;

    #endregion Fields

    #region IObserver

    public void OnCompleted()
    {
        Console.WriteLine("Real time measurement stream has been terminated. ");
        _ = ApiRealTimeMeasurementWatchdog.RestartListener();
    }

    public void OnError(Exception error)
    {
        Console.WriteLine($"An error occured: {error}");
        _ = ApiRealTimeMeasurementWatchdog.RestartListener();
    }

    public async void OnNext(RealTimeMeasurement value)
    {
        try
        {
            var dbContext = GetDbContext();

            var realTimeMeasurementExtention = new RealTimeMeasurementExtention(value);

            CalcAvgOfLastSeconds(realTimeMeasurementExtention);

            ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1, Key = "AvgPowerConsumption", Value = realTimeMeasurementExtention.AvgPowerConsumption.ToString() });
            ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 2, Key = "AvgPowerProduction", Value = realTimeMeasurementExtention.AvgPowerProduction.ToString() });

            lock (ApiServiceInfo.RealTimeMeasurement._syncRoot)
            {
                ApiServiceInfo.RealTimeMeasurement.Add(realTimeMeasurementExtention);
            }

            dbContext.RealTimeMeasurements.Add(realTimeMeasurementExtention); // Speichern in der Datenbank

            await dbContext.SaveChangesAsync(); // Änderungen speichern

            await RealTimeMeasurement(realTimeMeasurementExtention);

            if (realTimeMeasurementExtention.RequestedPowerValue.HasValue)
                Logger.LogDebug($"RequestedPowerValue: {realTimeMeasurementExtention.RequestedPowerValue}");

            realTimeMeasurementExtention.SettingLockSeconds = ApiServiceInfo.SettingLockSeconds;
            realTimeMeasurementExtention.SettingPowerLoadSeconds = ApiServiceInfo.SettingAvgPowerLoadSeconds;
            realTimeMeasurementExtention.SettingOffSetAvg = ApiServiceInfo.SettingAvgPowerOffset;
            realTimeMeasurementExtention.SettingToleranceAvg = ApiServiceInfo.SettingAvgPowerHysteresis;

            await dbContext.SaveChangesAsync(); // Änderungen speichern
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }
    }

    #endregion IObserver

    #region Ctor

    private bool isDisposed = false;

    public ApiService(IServiceProvider serviceProvider, ApiServiceInfo apiServiceInfo, ApiRealTimeMeasurementWatchdog apiRealTimeMeasurementWatchdog)
    {
        ServiceProvider = serviceProvider;
        ApiServiceInfo = apiServiceInfo;
        ApiRealTimeMeasurementWatchdog = apiRealTimeMeasurementWatchdog;

        ApiServiceInfo.RefreshDeviceList += RefreshDeviceList;
        ApiServiceInfo.RefreshNoahs += RefreshDeviceNoahs;
        ApiServiceInfo.RefreshNoahLastData += RefreshDeviceNoahLastData;
        ApiServiceInfo.ClearDeviceNoahTimeSegments += ClearDeviceNoahTimeSegments;
        DeviceQueryQueueWatchdog.OnItemDequeued += DeviceQueryQueueWatchdog_OnItemDequeued;
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion Ctor

    #region Events

    private async Task ClearDeviceNoahTimeSegments(object sender, EventArgs e)
    {
        await SetNoDeviceNoahTimeSegmentsAsync();
    }

    private async Task RefreshDeviceList(object sender, EventArgs e)
    {
        await GetDevice();
        await RefreshDeviceNoahs(sender, e);
    }

    private Task RefreshDeviceNoahLastData(object sender, EventArgs e)
    {
        GetDeviceNoahLastData();

        return Task.CompletedTask;
    }

    private Task RefreshDeviceNoahs(object sender, EventArgs e)
    {
        GetDeviceNoahInfo();
        GetDeviceNoahLastData();

        return Task.CompletedTask;
    }

    #endregion Events

    #region Properties

    private bool isRealTimeMeasurementRunning = false;
    public ApiServiceInfo ApiServiceInfo { get; set; }
    public bool DeviceNoahIsOffline { get; set; }
    public ApiQueueWatchdog<IDeviceQuery> DeviceQueryQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<IDeviceQuery>>();

    private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog { get; set; }

    private ILogger Logger => ServiceProvider.GetRequiredService<ILogger<ApiService>>();
    private IServiceProvider ServiceProvider { get; set; }

    #endregion Properties

    #region Methodes

    public async Task LoadApiServiceInfoFromDatabase()
    {
        var dbContext = GetDbContext();

        var devices = await dbContext.Devices.ToListAsync();
        ApiServiceInfo.Devices.Clear();
        foreach (var device in devices)
        {
            ApiServiceInfo.Devices.Add(device);
        }

        var deviceNoahInfoList = await dbContext.DeviceNoahInfo.ToListAsync();
        ApiServiceInfo.DeviceNoahInfo.Clear();
        foreach (var info in deviceNoahInfoList)
        {
            ApiServiceInfo.DeviceNoahInfo.Add(info);
        }

        var deviceNoahLastDataList = await dbContext.DeviceNoahLastData.ToListAsync();
        ApiServiceInfo.DeviceNoahLastData.Clear();
        foreach (var lastData in deviceNoahLastDataList)
        {
            ApiServiceInfo.DeviceNoahLastData.Add(lastData);
        }

        var realTimeMeasurements = await dbContext.RealTimeMeasurements.OrderByDescending(x => x.Timestamp).Take(100).ToListAsync();
        ApiServiceInfo.RealTimeMeasurement.Clear();
        foreach (var measurement in realTimeMeasurements)
        {
            ApiServiceInfo.RealTimeMeasurement.Add(measurement);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tibberClient = ServiceProvider.GetRequiredService<TibberApiClient>();

        var basicData = await tibberClient.GetBasicData(cancellationToken);
        TibberHomeId = basicData.Data.Viewer.Homes.FirstOrDefault()?.Id;

        await GetTomorrowPrices();
        await LoadApiServiceInfoFromDatabase();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }

    private ApplicationDbContext GetDbContext()
    {
        return ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    #region Tibber

    private Guid? TibberHomeId { get; set; }

    public void CalcAvgOfLastSeconds(RealTimeMeasurementExtention value)
    {
        var now = DateTimeOffset.Now;

        lock (ApiServiceInfo.RealTimeMeasurement._syncRoot)
        {
            var resultPower = ApiServiceInfo.RealTimeMeasurement
                .Where(m => m.Timestamp >= now.AddSeconds(-ApiServiceInfo.SettingAvgPowerLoadSeconds) && m.Power > 0)
                .Select(m => m.Power);

            value.AvgPowerConsumption = value.Power > 0 ? resultPower.Any() ? (int)resultPower.Average() : 0 : 0;

            var resultPowerProduction = ApiServiceInfo.RealTimeMeasurement
                .Where(m => m.Timestamp >= now.AddSeconds(-ApiServiceInfo.SettingAvgPowerLoadSeconds) && m.PowerProduction != null && m.PowerProduction > 0)
                .Select(m => -(int)(m.PowerProduction));

            value.AvgPowerProduction = value.PowerProduction > 0 ? resultPowerProduction.Any() ? (int)resultPowerProduction.Average() : 0 : 0;
        }
    }

    public async Task GetDataFromTibber()
    {
        try
        {
            var tibberApiClient = ServiceProvider.GetRequiredService<TibberApiClient>();

            var basicData = await tibberApiClient.GetBasicData();
            TibberHomeId = basicData.Data.Viewer.Homes.FirstOrDefault()?.Id;

            if (TibberHomeId.HasValue)
            {
                var consumption = await tibberApiClient.GetHomeConsumption(TibberHomeId.Value, EnergyResolution.Monthly);

                var customQueryBuilder =
                    new TibberQueryBuilder()
                        .WithAllScalarFields()
                        .WithViewer(
                            new ViewerQueryBuilder()
                                .WithAllScalarFields()
                                .WithAccountType()
                                .WithHome(
                                    new HomeQueryBuilder()
                                        .WithAllScalarFields()
                                        .WithAddress(new AddressQueryBuilder().WithAllFields())
                                        .WithCurrentSubscription(
                                            new SubscriptionQueryBuilder()
                                                .WithAllScalarFields()
                                                .WithSubscriber(new LegalEntityQueryBuilder().WithAllFields())
                                                .WithPriceInfo(new PriceInfoQueryBuilder().WithCurrent(new PriceQueryBuilder().WithAllFields()))
                                        )
                                        .WithOwner(new LegalEntityQueryBuilder().WithAllFields())
                                        .WithFeatures(new HomeFeaturesQueryBuilder().WithAllFields())
                                        .WithMeteringPointData(new MeteringPointDataQueryBuilder().WithAllFields()),
                                    TibberHomeId
                                )
                        );

                var customQuery = customQueryBuilder.Build();
                var result = await tibberApiClient.Query(customQuery);

                var query = new TibberQueryBuilder().WithHomeConsumption(TibberHomeId.Value, EnergyResolution.Monthly, 12).Build();
                var response = await tibberApiClient.Query(query);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public async Task GetTomorrowPrices()
    {
        try
        {
            if (TibberHomeId.HasValue)
            {
                // Erstellung der benutzerdefinierten Abfrage mit dem TibberQueryBuilder
                var customQueryBuilder =
                new TibberQueryBuilder()
                    .WithViewer(
                        new ViewerQueryBuilder()
                        .WithHome(
                            new HomeQueryBuilder()
                            .WithCurrentSubscription(
                                new SubscriptionQueryBuilder()
                                    .WithPriceInfo(
                                        new PriceInfoQueryBuilder()
                                        .WithAllScalarFields()
                                        .WithToday(
                                            new PriceQueryBuilder()
                                            .WithAllScalarFields()
                                        )
                                        .WithTomorrow(
                                            new PriceQueryBuilder()
                                            .WithAllScalarFields()
                                        )
                                    )
                            ),
                            TibberHomeId
                        )
                    );

                // Abfrage ausführen
                var customQuery = customQueryBuilder.Build(); // Erzeugt den GraphQL-Abfragetext
                var result = await ServiceProvider.GetRequiredService<TibberApiClient>().Query(customQuery);

                await SavePrices(result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Tomorrow.Select(x => new ApiPrice()
                {
                    StartsAt = DateTime.Parse(x.StartsAt),
                    Total = x.Total,
                    Level = x.Level
                }).ToList()
                );
                await SavePrices(result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Today.Select(x => new ApiPrice()
                {
                    StartsAt = DateTime.Parse(x.StartsAt),
                    Total = x.Total,
                    Level = x.Level
                }).ToList()
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task SavePrices(IList<ApiPrice> prices)
    {
        var dbContext = GetDbContext();
        var topPrices = prices.OrderByDescending(x => x.Total).Take(10).ToList();
        var avg = topPrices.Average(x => x.Total);

        // Verarbeitung der Ergebnisse
        foreach (var price in prices)
        {
            price.AutoModeRestriction = price.Total > avg;

            Console.WriteLine($"Zeit: {price.StartsAt}, Preis: {price.Total} EUR/kWh");

            //Prüfe ob es denn eintrag schon gibt und falls ja mach ein update

            ApiServiceInfo.Prices.Add(price);

            // Prüfe, ob der Datensatz bereits existiert
            var existingDevice = await dbContext.Prices.FindAsync(price.StartsAt);
            if (existingDevice != null)
            {
                // Aktualisiere den bestehenden Datensatz
                dbContext.Entry(existingDevice).CurrentValues.SetValues(price);
            }
            else
            {
                // Füge den neuen Datensatz hinzu
                dbContext.Prices.Add(price);
            }
        }
        await dbContext.SaveChangesAsync(); // Änderungen speichern
    }

    #region Regulation

    private readonly Lock lockAdjustPower = new();

    private void AdjustPower(RealTimeMeasurementExtention value)
    {
        int calcPowerValue = 0;
        int newPowerValue = 0;

        var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah" && x.IsOfflineSince == null).ToList();

        Device? device = null;

        var last = devices.OrderBy(x => x.PowerValueLastChanged).FirstOrDefault();

        var upperlimit = ApiServiceInfo.SettingAvgPowerOffset + (ApiServiceInfo.SettingAvgPowerHysteresis / 2);
        var lowerlimit = ApiServiceInfo.SettingAvgPowerOffset - (ApiServiceInfo.SettingAvgPowerHysteresis / 2);

        int consumptionDelta = 0;
        int productionDelta = 0;

        if (last == null || last.PowerValueRequested == last.PowerValueCommited)
        {
            if (value.TotalPower > 0)
                device = devices.OrderBy(x => x.PowerValueCommited).FirstOrDefault();

            if (value.TotalPower < 0)
                device = devices.OrderByDescending(x => x.PowerValueCommited).FirstOrDefault();

            if (device != null)
            {
                int lastCommitedPowerValue = device.PowerValueCommited == 0 ? (int)(ApiServiceInfo.GetNoahLastDataPerDevice(device.DeviceSn)?.pac ?? 0) : device.PowerValueCommited;
                var lastRequestedPowerValue = device.PowerValueRequested;

                // If the total power is greater than 0, it indicates power consumption
                if (value.TotalPower > 0)
                {
                    // If the average power consumption is greater than the upper limit
                    if (value.AvgPowerConsumption > upperlimit)
                    {
                        // Calculate the difference between the average power consumption and the
                        // upper limit
                        consumptionDelta = Math.Abs(value.AvgPowerConsumption - upperlimit);
                        // Add or update the trace value for the delta power value
                        ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 4, Key = "DeltaPowerValue", Value = consumptionDelta.ToString() });
                        // Calculate the new power value based on the consumption delta
                        calcPowerValue = lastCommitedPowerValue + (consumptionDelta / devices.Count);
                    }
                    // If the average power consumption is less than the lower limit
                    else if (value.AvgPowerConsumption < lowerlimit)
                    {
                        // Calculate the difference between the lower limit and the average power consumption
                        consumptionDelta = Math.Abs(lowerlimit - value.AvgPowerConsumption);
                        // Calculate the new power value based on the consumption delta
                        calcPowerValue = lastCommitedPowerValue - (consumptionDelta / devices.Count);
                    }
                }
                // If the total power is less than 0, it indicates power production
                else if (value.TotalPower < 0)
                {
                    // If the average power production is less than the lower limit
                    if (value.AvgPowerProduction < lowerlimit)
                    {
                        // Calculate the difference between the average power production and the
                        // lower limit
                        productionDelta = Math.Abs(value.AvgPowerProduction - lowerlimit);
                        // Add or update the trace value for the delta power value
                        ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 4, Key = "DeltaPowerValue", Value = productionDelta.ToString() });
                        // Calculate the new power value based on the production delta
                        calcPowerValue = lastCommitedPowerValue - (productionDelta / devices.Count);
                    }
                    // If the average power production is greater than the upper limit
                    else if (value.AvgPowerProduction > upperlimit)
                    {
                        // Calculate the difference between the average power production and the
                        // upper limit
                        productionDelta = Math.Abs(value.AvgPowerProduction - upperlimit);
                        // Calculate the new power value based on the production delta
                        calcPowerValue = lastCommitedPowerValue + (productionDelta / devices.Count);
                    }
                }

                var maxPower = ApiServiceInfo.SettingMaxPower / devices.Count;

                newPowerValue = calcPowerValue > maxPower ? maxPower : calcPowerValue < 0 ? 0 : calcPowerValue;

                if (newPowerValue <= maxPower && newPowerValue > 0)
                {
                    if (newPowerValue != lastRequestedPowerValue)
                    {
                        device.PowerValueLastChanged = value.TS;
                        device.PowerValueRequested = newPowerValue;
                        value.RequestedPowerValue = newPowerValue;

                        DeviceQueryQueueWatchdog.Enqueue(new DeviceNoahSetPowerQuery()
                        {
                            DeviceType = "noah",
                            DeviceSn = device.DeviceSn,
                            Value = newPowerValue,
                            TS = value.TS
                        });

                        ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 5, Key = "NewPowerValue", Value = newPowerValue.ToString() });
                    }
                }
                else
                {
                    if (value.TotalPower > 0)
                    {
                        Trace.WriteLine($"TotalPower: {value.TotalPower}, AvgPowerProduction: {value.AvgPowerConsumption}, upperDelta: {consumptionDelta} = {value.AvgPowerConsumption} - {upperlimit}", "ApiService");
                        Trace.WriteLine($"lastCommitedPowerValue: {lastCommitedPowerValue}, upperDelta: {consumptionDelta} = {value.AvgPowerConsumption} - {upperlimit}, calcPowerValue: {calcPowerValue}, OffSet: {ApiServiceInfo.SettingAvgPowerOffset}", "ApiService");
                    }
                    if (value.TotalPower < 0)
                    {
                        Trace.WriteLine($"TotalPower: {value.TotalPower}, AvgPowerProduction: {value.AvgPowerProduction}, lowerDelta: {productionDelta} = {value.AvgPowerProduction} - {lowerlimit}", "ApiService");
                        Trace.WriteLine($"lastCommitedPowerValue: {lastCommitedPowerValue} - lowerDelta: {productionDelta}, calcPowerValue: {calcPowerValue}, OffSet: {ApiServiceInfo.SettingAvgPowerOffset}", "ApiService");
                    }
                }

                ApiServiceInfo.InvokeStateHasChanged();
            }
        }
    }

    private async Task DisableAutoMode(RealTimeMeasurementExtention value)
    {
        DeviceQueryQueueWatchdog.Clear();

        await ClearPowerSetAsync();
        await SetBattPriorityDeviceNoahTimeSegmentsAsync();
    }

    private async Task EnableAutoMode(RealTimeMeasurementExtention value)
    {
        GetDeviceNoahLastData();
        await SetNoDeviceNoahTimeSegmentsAsync();
    }

    private async Task RealTimeMeasurement(RealTimeMeasurementExtention value)
    {
        if (ApiServiceInfo.SettingAutoMode)
        {
            if (!isRealTimeMeasurementRunning)
            {
                isRealTimeMeasurementRunning = true;
                await EnableAutoMode(value);
            }

            if (ApiServiceInfo.SettingAutoModeRestriction)
            {
                if (ApiServiceInfo.GetCurrentAutoModeRestriction())
                {
                    lock (lockAdjustPower)
                    {
                        AdjustPower(value);
                    }
                }
                else
                {
                    Trace.WriteLine($"Not in grace periode: Nothing to do", "ApiService");
                }
            }
            else
            {
                lock (lockAdjustPower)
                {
                    AdjustPower(value);
                }
            }

            ProceedRules();
        }
        else
        {
            if (isRealTimeMeasurementRunning)
            {
                isRealTimeMeasurementRunning = false;
                await DisableAutoMode(value);
            }
        }
    }

    #endregion Regulation

    #endregion Tibber

    #region Growatt

    #region Configs

    private Task ClearDeviceNoahTimeSegmentsAsync(Queue<IDeviceQuery> DeviceTimeSegmentQueue)
    {
        var deviceSnList = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

        GetDeviceNoahInfo();

        foreach (var deviceSn in deviceSnList)
        {
            var data = ApiServiceInfo.DeviceNoahInfo.FirstOrDefault(x => x.DeviceSn == deviceSn);
            if (data != null)
            {
                var enabledSegments = data.TimeSegments.Where(x => x.Enable == "1").ToList();

                foreach (var segment in enabledSegments)
                {
                    var request = new DeviceNoahTimeSegmentQuery
                    {
                        Force = true,
                        DeviceSn = deviceSn,
                        DeviceType = "noah",
                        Type = segment.Type,
                        StartTime = "0:0",
                        EndTime = "23:59",
                        Mode = "0",
                        Power = "0",
                        Enable = "0"
                    };

                    DeviceTimeSegmentQueue.Enqueue(request);
                }
            }
        }

        return Task.CompletedTask;
    }

    private Task ClearPowerSetAsync(int value = 0)
    {
        var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").ToList();

        foreach (var device in devices)
        {
            DeviceQueryQueueWatchdog.Enqueue(new DeviceNoahSetPowerQuery()
            {
                DeviceType = "noah",
                DeviceSn = device.DeviceSn,
                Value = value,
                Force = true
            });
        }

        return Task.CompletedTask;
    }

    private async Task SetBattPriorityDeviceNoahTimeSegmentsAsync()
    {
        Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

        await ClearDeviceNoahTimeSegmentsAsync(DeviceTimeSegmentQueue);

        var deviceSnList = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

        foreach (var deviceSn in deviceSnList)
        {
            DeviceTimeSegmentQueue.Enqueue(new DeviceNoahTimeSegmentQuery()
            {
                Force = true,
                DeviceSn = deviceSn,
                DeviceType = "noah",
                Type = "1",
                StartTime = "8:0",
                EndTime = "15:59",
                Mode = "1",
                Power = "0",
                Enable = "1"
            });
        }

        DeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
    }

    private async Task SetLoadPriorityDeviceNoahTimeSegmentsAsync()
    {
        Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

        await ClearDeviceNoahTimeSegmentsAsync(DeviceTimeSegmentQueue);

        var deviceSnList = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

        foreach (var deviceSn in deviceSnList)
        {
            DeviceTimeSegmentQueue.Enqueue(new DeviceNoahTimeSegmentQuery()
            {
                Force = true,
                DeviceSn = deviceSn,
                DeviceType = "noah",
                Type = "1",
                StartTime = "8:0",
                EndTime = "23:59",
                Mode = "0",
                Power = "0",
                Enable = "1"
            });
        }

        DeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
    }

    private async Task SetNoDeviceNoahTimeSegmentsAsync()
    {
        Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

        await ClearDeviceNoahTimeSegmentsAsync(DeviceTimeSegmentQueue);

        DeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
    }

    #endregion Configs

    #region Functions

    private async Task<ApiException?> DeviceQueryQueueWatchdog_OnItemDequeued(IDeviceQuery item, GrowattApiClient growattApiClient)
    {
        var dbContext = GetDbContext();
        RealTimeMeasurementExtention? dataRealTimeMeasurementApiServiceInfo = null;
        RealTimeMeasurementExtention? dataRealTimeMeasurementDbContext = null;
        Device? device = null;

        var entry = new ApiCallLog()
        {
            MethodeName = item.GetType().Name,
            TimeStamp = DateTime.Now,
            RaisedError = false
        };

        switch (item)
        {
            case DeviceNoahSetPowerQuery setPowerQuery:
                if (setPowerQuery != null && !string.IsNullOrWhiteSpace(setPowerQuery.DeviceType) && !string.IsNullOrWhiteSpace(setPowerQuery.DeviceSn))
                {
                    device = ApiServiceInfo.Devices.FirstOrDefault(x => x.DeviceSn == setPowerQuery.DeviceSn);
                    if (setPowerQuery.TS.HasValue)
                    {
                        dataRealTimeMeasurementApiServiceInfo = ApiServiceInfo.RealTimeMeasurement.FirstOrDefault(x => x.TS == setPowerQuery.TS);
                        dataRealTimeMeasurementDbContext = dbContext.RealTimeMeasurements.FirstOrDefault(x => x.TS == setPowerQuery.TS);
                    }

                    try
                    {
                        //Add log entry
                        ApiServiceInfo.DataReads.Add(entry);

                        await growattApiClient.SetPowerAsync(item);

                        if (device != null)
                            lock (ApiServiceInfo.Devices._syncRoot)
                                device.PowerValueCommited = setPowerQuery.Value;

                        if (dataRealTimeMeasurementApiServiceInfo != null)
                            lock (ApiServiceInfo.RealTimeMeasurement._syncRoot)
                                dataRealTimeMeasurementApiServiceInfo.CommitedPowerValue = setPowerQuery.Value;

                        if (dataRealTimeMeasurementDbContext != null)
                        {
                            dataRealTimeMeasurementDbContext.CommitedPowerValue = setPowerQuery.Value;
                            await dbContext.SaveChangesAsync();
                        }

                        Trace.WriteLine($"Commited lastRequestedPowerValue: {setPowerQuery.Value} W", "ApiService");

                        ApiServiceInfo.InvokeStateHasChanged();

                        return default; // Operation erfolgreich
                    }
                    catch (ApiException ex)
                    {
                        return ex; // Operation fehlgeschlagen
                    }
                }
                return default; ;
            case DeviceNoahTimeSegmentQuery timeSegmentQuery:

                if (timeSegmentQuery != null && !string.IsNullOrWhiteSpace(timeSegmentQuery.DeviceType) && !string.IsNullOrWhiteSpace(timeSegmentQuery.DeviceSn))
                {
                    try
                    {
                        //Add log entry
                        ApiServiceInfo.DataReads.Add(entry);

                        await growattApiClient.SetTimeSegmentAsync(item);

                        return default; // Operation erfolgreich
                    }
                    catch (ApiException ex)
                    {
                        return ex; // Operation fehlgeschlagen
                    }
                }
                return default;

            case DeviceNoahLastDataQuery lastDataQuery:

                if (lastDataQuery != null && !string.IsNullOrWhiteSpace(lastDataQuery.DeviceType) && !string.IsNullOrWhiteSpace(lastDataQuery.DeviceSn))
                {
                    try
                    {
                        if (ApiServiceInfo.DataReadsDoRefresh(item.GetType().Name))
                        {
                            //Add log entry
                            ApiServiceInfo.DataReads.Add(entry);

                            //Refresh Last data ever minute
                            var deviceNoahLastDatas = await growattApiClient.GetDeviceLastDataAsync(lastDataQuery);
                            if (deviceNoahLastDatas != null)
                            {
                                await SaveDeviceNoahLastData(deviceNoahLastDatas);
                            }
                        }

                        return default; // Operation erfolgreich
                    }
                    catch (ApiException ex)
                    {
                        return ex; // Operation fehlgeschlagen
                    }
                }
                return default;

            case DeviceNoahInfoQuery infoQuery:

                if (item != null && !string.IsNullOrWhiteSpace(item.DeviceType) && !string.IsNullOrWhiteSpace(item.DeviceSn))
                {
                    try
                    {
                        //if (_demoMode) return default;
                        if (ApiServiceInfo.DataReadsDoRefresh(item.GetType().Name))
                        {
                            //Add log entry
                            ApiServiceInfo.DataReads.Add(entry);

                            //Refresh Last data ever minute
                            var deviceNoahInfos = await growattApiClient.GetDeviceInfoAsync(infoQuery);
                            if (deviceNoahInfos != null)
                            {
                                await SaveDeviceNoahInfo(deviceNoahInfos);
                            }
                        }

                        return default; // Operation erfolgreich
                    }
                    catch (ApiException ex)
                    {
                        return ex; // Operation fehlgeschlagen
                    }
                }
                return default;

            default:
                return default;
        }
    }

    #endregion Functions

    #region Basics

    private GrowattApiClient GrowattApiClient => ServiceProvider.GetRequiredService<GrowattApiClient>();

    public async Task GetDevice()
    {
        var dbContext = GetDbContext();

        List<Device>? devices = null;

        while (devices == null)
        {
            try
            {
                devices = await GrowattApiClient.GetDeviceListAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);

                await Task.Delay(ApiServiceInfo.SettingLockSeconds);
            }
        }

        if (devices != null)
        {
            foreach (var device in devices)
            {
                ApiServiceInfo.Devices.Add(device);

                var existingDevice = await dbContext.Devices.FindAsync(device.DeviceSn);
                if (existingDevice != null)
                {
                    dbContext.Entry(existingDevice).CurrentValues.SetValues(device);
                }
                else
                {
                    dbContext.Devices.Add(device);
                }
            }
            await dbContext.SaveChangesAsync();
        }
    }

    public void GetDeviceNoahInfo()
    {
        var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").ToList();

        if (IsOfflineNull(devices))
        {
            var deviceSnList = string.Join(",", devices.Select(x => x.DeviceSn).ToList());

            DeviceQueryQueueWatchdog.Enqueue(new DeviceNoahLastDataQuery()
            {
                DeviceType = "noah",
                DeviceSn = deviceSnList,
            });
        }
    }

    public void GetDeviceNoahLastData()
    {
        var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").ToList();

        if (IsOfflineNull(devices))
        {
            var deviceSnList = string.Join(",", devices.Select(x => x.DeviceSn).ToList());

            DeviceQueryQueueWatchdog.Enqueue(new DeviceNoahLastDataQuery()
            {
                DeviceType = "noah",
                DeviceSn = deviceSnList,
            });
        }
    }

    private bool IsOfflineNull(List<Device> devices)
    {
        foreach (var device in devices)
        {
            var itemApiServiceInfoDevices = ApiServiceInfo.Devices.FirstOrDefault(x => x.DeviceSn == device.DeviceSn);
            if (itemApiServiceInfoDevices?.IsOfflineSince != null) return false;
        }
        return true;
    }

    private void ProceedRules()
    {
        //Refresh Last data ever minute
        if (ApiServiceInfo.DataReadsDoRefresh(nameof(DeviceNoahInfoQuery)))
        {
            GetDeviceNoahInfo();
        }

        //Refresh Last data ever minute
        if (ApiServiceInfo.DataReadsDoRefresh(nameof(DeviceNoahLastDataQuery)))
        {
            GetDeviceNoahLastData();
        }
    }

    private async Task SaveDeviceNoahInfo(List<DeviceNoahInfo> deviceNoahInfos)
    {
        var dbContext = GetDbContext();

        foreach (var deviceNoahInfo in deviceNoahInfos)
        {
            var deviceApiServiceInfo = ApiServiceInfo.Devices.FirstOrDefault(x => x.DeviceType == "noah" && x.DeviceSn == deviceNoahInfo.DeviceSn);
            var deviceDbContext = dbContext.Devices.FirstOrDefault(x => x.DeviceType == "noah" && x.DeviceSn == deviceNoahInfo.DeviceSn);

            if (deviceDbContext != null)
            {
                deviceDbContext.IsOfflineSince = deviceNoahInfo.Lost ? new DateTime(deviceNoahInfo.LastUpdateTime) : null;
            }

            if (deviceApiServiceInfo != null)
            {
                deviceApiServiceInfo.IsOfflineSince = deviceNoahInfo.Lost ? new DateTime(deviceNoahInfo.LastUpdateTime) : null;
            }

            ApiServiceInfo.DeviceNoahInfo.Add(deviceNoahInfo);

            var existingDeviceNoahInfo = await dbContext.DeviceNoahInfo.FindAsync(deviceNoahInfo.DeviceSn);
            if (existingDeviceNoahInfo != null)
            {
                dbContext.Entry(existingDeviceNoahInfo).CurrentValues.SetValues(deviceNoahInfo);
            }
            else
            {
                dbContext.DeviceNoahInfo.Add(deviceNoahInfo);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task SaveDeviceNoahLastData(List<DeviceNoahLastData> deviceNoahLastDatas)
    {
        var dbContext = GetDbContext();

        foreach (var item in deviceNoahLastDatas)
        {
            var device = ApiServiceInfo.Devices.FirstOrDefault(x => x.DeviceSn == item.deviceSn);
            if (device != null)
            {
                device.PowerValueRequested = (int)item.pac;
                device.PowerValueCommited = (int)item.pac;
            }

            var deviceDbContext = await dbContext.Devices.FindAsync(item.deviceSn);
            if (deviceDbContext != null)
            {
                deviceDbContext.PowerValueRequested = (int)item.pac;
                deviceDbContext.PowerValueCommited = (int)item.pac;
            }

            ApiServiceInfo.DeviceNoahLastData.Add(item);

            var existingDeviceNoahLastData = await dbContext.DeviceNoahLastData.FindAsync(item.deviceSn, item.time);
            if (existingDeviceNoahLastData != null)
            {
                dbContext.Entry(existingDeviceNoahLastData).CurrentValues.SetValues(item);
            }
            else
            {
                dbContext.DeviceNoahLastData.Add(item);
            }
        }

        await dbContext.SaveChangesAsync();

        DeviceNoahIsOffline = ApiServiceInfo.Devices.Any(x => x.DeviceType == "noah" && x.IsOfflineSince != null);
    }

    #endregion Basics

    #endregion Growatt

    #endregion Methodes
}
