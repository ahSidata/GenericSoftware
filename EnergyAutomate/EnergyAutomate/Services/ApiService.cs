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
        DeviceNoahTimeSegmentQueueWatchdog.OnItemDequeued += DeviceNoahTimeSegmentQueueWatchdog_OnItemDequeued;
        DeviceNoahLastDataQueueWatchdog.OnItemDequeued += DeviceNoahLastDataQueueWatchdog_OnItemDequeued;
        DeviceNoahOutputValueQueueWatchdog.OnItemDequeued += DeviceNoahOutputValueQueueWatchdog_OnItemDequeued;
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
    public ApiQueueWatchdog<DeviceNoahLastDataQuery> DeviceNoahLastDataQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahLastDataQuery>>();
    public ApiQueueWatchdog<DeviceNoahOutputValueQuery> DeviceNoahOutputValueQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahOutputValueQuery>>();
    public ApiQueueWatchdog<DeviceNoahTimeSegmentQuery> DeviceNoahTimeSegmentQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahTimeSegmentQuery>>();
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

            value.AvgPowerConsumption = resultPower.Any() ? (int)resultPower.Average() : 0;

            var resultPowerProduction = ApiServiceInfo.RealTimeMeasurement
                .Where(m => m.Timestamp >= now.AddSeconds(-ApiServiceInfo.SettingAvgPowerLoadSeconds) && m.PowerProduction != null && m.PowerProduction > 0)
                .Select(m => -(int)(m.PowerProduction));

            value.AvgPowerProduction = resultPowerProduction.Any() ? (int)resultPowerProduction.Average() : 0;
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

    //// Basis-Schrittweite für die Anpassung des eingespeisten Stroms
    ///// <summary>
    ///// Adjusts the power output based on the difference between the current power consumption and
    ///// the target power consumption. The adjustment takes into account both the magnitude and
    ///// duration of the deviation to determine the adjustment step size.
    ///// </summary>
    ///// <param name="state">The state object passed by the Timer (not used).</param>
    //private void AdjustPower1(RealTimeMeasurementExtention value)
    //{
    //    var deviceCount = ApiServiceInfo.GetNoahDeviceCount();
    //    var currentPowerValueSum = ApiServiceInfo.GetNoahCurrentPowerValueSum();

    // var targetConsumption = ApiServiceInfo.SettingAvgPowerOffset;

    // var differenceConsumption = Math.Abs(value.AvgPowerConsumption - targetConsumption); var
    // differenceProduction = Math.Abs(value.AvgPowerProduction - targetConsumption);

    // // true if it is discharging var state = ApiServiceInfo.GetNoahCurrentIsDischarchingState();
    // var isDischaring = state > 1;

    // // Überproduktion prüfen: Wenn der Strom positiv ist und es keine Entladung gibt var
    // isOverproduction = currentPowerValueSum > 0 && !isDischaring;

    // // Wenn es eine Überproduktion gibt, keine Anpassung vornehmen if (isOverproduction) {
    // Logger.LogTrace("Overproduction detected, no adjustment needed."); return; }

    // // Wenn die Akkus entladen werden, keine Anpassung vornehmen if (isDischaring) {
    // Logger.LogTrace("Batteries are in use."); }

    // // Letzten gesetzten Power-Wert holen var lastCommitedPowerValue =
    // ApiServiceInfo.GetLastCommitedPowerValue() ?? currentPowerValueSum;

    // if (lastCommitedPowerValue < 0) lastCommitedPowerValue = 0;

    // Logger.LogTrace($"Values >> readLastCommitedPowerValue:
    // {ApiServiceInfo.GetLastCommitedPowerValue()} W, currentPowerValueSum: {currentPowerValueSum}
    // W, newLastCommitedPowerValue: {lastCommitedPowerValue} W");

    // // Adaptive Hysterese: Nur reagieren, wenn die Abweichung groß genug ist var
    // dynamicHysteresis = ApiServiceInfo.SettingAvgPowerHysteresis * (1 + Math.Log10(1 + difference));

    // Logger.LogTrace($"CalcPower >> CurrentConsumption: {value.AvgPowerLoad} W, Offset:
    // {ApiServiceInfo.SettingAvgPowerOffset}, Diff: {difference}, Hysteresis:
    // {dynamicHysteresis}"); if (difference > dynamicHysteresis) { var timeSinceLastAdjustment =
    // (DateTime.Now - ApiServiceInfo.SettingAvgPowerlastAdjustmentTime).TotalSeconds;

    // // Logarithmische Anpassung für sanftes Nachregeln var adjustmentFactor =
    // Math.Log10(difference + 1) * (timeSinceLastAdjustment / 10); adjustmentFactor =
    // Math.Clamp(adjustmentFactor, 0.1, 10); // Begrenzung für Stabilität

    // // Berechnung des neuen Werts basierend auf dem letzten Wert var adjustmentStep =
    // (int)(ApiServiceInfo.SettingAvgPowerAdjustmentStep * adjustmentFactor); int newPowerValue =
    // lastCommitedPowerValue + (difference > 0 ? -adjustmentStep : adjustmentStep);
    // Logger.LogTrace($"AdjustPower >> TimeSinceLastAdjustment: {timeSinceLastAdjustment} sec,
    // adjustmentFactor: {adjustmentFactor}, adjustmentStep: {adjustmentStep}, Hysteresis: {dynamicHysteresis}");

    // // Begrenzung auf max. zulässige Leistung mit Logging if (newPowerValue >
    // ApiServiceInfo.SettingMaxPower) { Logger.LogTrace($"MaxPower override: {newPowerValue} >>
    // {ApiServiceInfo.SettingMaxPower} W"); newPowerValue = ApiServiceInfo.SettingMaxPower; }

    // // Sicherstellen, dass der neue Power-Wert durch die Anzahl der Geräte teilbar ist if
    // (deviceCount > 0) { int remainder = newPowerValue % deviceCount; if (remainder != 0) { //
    // Falls Rest vorhanden, anpassen newPowerValue -= remainder; } }

    // // Setzen den neuen angeforderten Wertes falls er sich geändert hat if (newPowerValue !=
    // ApiServiceInfo.GetLastRequestedPowerValue()) { value.RequestedPowerValue = newPowerValue;
    // ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.Add(new APiTraceValue() { Index = 3, Key
    // = "NewPowerValue", Value = newPowerValue.ToString() }); Logger.LogTrace($"PowerChanged >>
    // NewPowerValue: {newPowerValue} W, Offset: {ApiServiceInfo.SettingAvgPowerOffset}, Diff:
    // {difference}, Hysteresis: {dynamicHysteresis}");

    // var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").ToList(); var
    // deviceSnList = string.Join(",", devices.Select(x => x.DeviceSn).ToList());
    // DeviceNoahOutputValueQueueWatchdog.Enqueue(new DeviceNoahOutputValueQuery() { DeviceType =
    // "noah", DeviceSn = deviceSnList, Value = newPowerValue, TS = value.TS }); }

    // // Update letzter Anpassungszeitpunkt ApiServiceInfo.SettingAvgPowerlastAdjustmentTime =
    // DateTime.Now; ApiServiceInfo.SettingAvgPowerLastDifference = difference;

    //        ApiServiceInfo.InvokeStateHasChanged();
    //    }
    //    else
    //    {
    //        Logger.LogTrace($"AdjustPower: {difference} < {dynamicHysteresis} Nothing to do");
    //    }
    //}

    private void AdjustPower(RealTimeMeasurementExtention value)
    {
        if (ApiServiceInfo.DataReadsDoRefresh(DeviceNoahLastDataQuery.QueryTypes.SetPowerAsync, 5))
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
                            // Calculate the difference between the average power consumption and
                            // the upper limit
                            consumptionDelta = Math.Abs(value.AvgPowerConsumption - upperlimit);
                            // Add or update the trace value for the delta power value
                            ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 4, Key = "DeltaPowerValue", Value = consumptionDelta.ToString() });
                            // Calculate the new power value based on the consumption delta
                            calcPowerValue = lastCommitedPowerValue + (consumptionDelta / devices.Count);
                        }
                        // If the average power consumption is less than the lower limit
                        else if (value.AvgPowerConsumption < lowerlimit)
                        {
                            // Calculate the difference between the lower limit and the average
                            // power consumption
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

                            DeviceNoahOutputValueQueueWatchdog.Enqueue(new DeviceNoahOutputValueQuery()
                            {
                                DeviceType = "noah",
                                DeviceSn = device.DeviceSn,
                                Value = newPowerValue,
                                TS = value.TS
                            });

                            ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 5, Key = "NewPowerValue", Value = newPowerValue.ToString() });
                            ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 6, Key = "RequestedPowerValue", Value = lastRequestedPowerValue.ToString() });
                            ApiServiceInfo.SettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 7, Key = "CommitedPowerValue", Value = lastCommitedPowerValue.ToString() });
                        }
                    }
                    else
                    {
                        if (value.TotalPower > 0)
                        {
                            Logger.LogTrace($"AvgPowerConsumption: {value.AvgPowerConsumption}");
                            Logger.LogTrace($"TotalPower: {value.TotalPower}, upperDelta: {consumptionDelta} = {value.AvgPowerConsumption} - {upperlimit}");
                            Logger.LogTrace($"lastCommitedPowerValue: {lastCommitedPowerValue}, upperDelta: {consumptionDelta} = {value.AvgPowerConsumption} - {upperlimit}");
                            Logger.LogTrace($"calcPowerValue: {calcPowerValue}, OffSet: {ApiServiceInfo.SettingAvgPowerOffset}");
                        }
                        if (value.TotalPower < 0)
                        {
                            Logger.LogTrace($"AvgPowerProduction: {value.AvgPowerProduction}");
                            Logger.LogTrace($"TotalPower: {value.TotalPower}, lowerDelta: {productionDelta} = {value.AvgPowerProduction} - {lowerlimit}");
                            Logger.LogTrace($"lastCommitedPowerValue: {lastCommitedPowerValue} - lowerDelta: {productionDelta}");
                            Logger.LogTrace($"calcPowerValue: {calcPowerValue}, OffSet: {ApiServiceInfo.SettingAvgPowerOffset}");
                        }
                    }

                    ApiServiceInfo.InvokeStateHasChanged();
                }
            }
        }
    }

    private async Task DisableAutoMode(RealTimeMeasurementExtention value)
    {
        DeviceNoahOutputValueQueueWatchdog.Clear();

        await ClearPowerSetAsync();
        await SetBattPriorityDeviceNoahTimeSegmentsAsync();
    }

    private async Task EnableAutoMode(RealTimeMeasurementExtention value)
    {
        if (ApiServiceInfo.DataReadsDoRefresh(DeviceNoahLastDataQuery.QueryTypes.DeviceNoahLastData))
        {
            GetDeviceNoahLastData();
        }

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
                    Logger.LogTrace($"Not in grace periode: Nothing to do");
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

    private Task ClearDeviceNoahTimeSegmentsAsync(Queue<DeviceNoahTimeSegmentQuery> DeviceTimeSegmentQueue)
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
            DeviceNoahOutputValueQueueWatchdog.Enqueue(new DeviceNoahOutputValueQuery()
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
        Queue<DeviceNoahTimeSegmentQuery> DeviceTimeSegmentQueue = new();

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

        DeviceNoahTimeSegmentQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
    }

    private async Task SetLoadPriorityDeviceNoahTimeSegmentsAsync()
    {
        Queue<DeviceNoahTimeSegmentQuery> DeviceTimeSegmentQueue = new();

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

        DeviceNoahTimeSegmentQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
    }

    private async Task SetNoDeviceNoahTimeSegmentsAsync()
    {
        Queue<DeviceNoahTimeSegmentQuery> DeviceTimeSegmentQueue = new();

        await ClearDeviceNoahTimeSegmentsAsync(DeviceTimeSegmentQueue);

        DeviceNoahTimeSegmentQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
    }

    #endregion Configs

    #region Functions

    private async Task<ApiException?> DeviceNoahLastDataQueueWatchdog_OnItemDequeued(DeviceNoahLastDataQuery item, GrowattApiClient growattApiClient)
    {
        if (item != null && !string.IsNullOrWhiteSpace(item.DeviceType) && !string.IsNullOrWhiteSpace(item.DeviceSn))
        {
            try
            {
                var entry = new ApiCallLog()
                {
                    MethodeName = item.QueryType,
                    TimeStamp = DateTime.Now,
                    RaisedError = false
                };

                //if (_demoMode) return default;
                if (ApiServiceInfo.DataReadsDoRefresh(item.QueryType))
                {
                    //Add log entry
                    ApiServiceInfo.DataReads.Add(entry);

                    switch (item.QueryType)
                    {
                        case DeviceNoahLastDataQuery.QueryTypes.DeviceNoahLastData:
                            //Refresh Last data ever minute
                            var deviceNoahLastDatas = await growattApiClient.GetDeviceLastDataAsync(item.DeviceType, item.DeviceSn);
                            if (deviceNoahLastDatas != null)
                            {
                                await SaveDeviceNoahLastData(deviceNoahLastDatas);
                            }

                            break;
                        case DeviceNoahLastDataQuery.QueryTypes.DeviceNoahInfo:
                            //Refresh Last data ever minute
                            var deviceNoahInfos = await growattApiClient.GetDeviceInfoAsync(item.DeviceType, item.DeviceSn);
                            if (deviceNoahInfos != null)
                            {
                                await SaveDeviceNoahInfo(deviceNoahInfos);
                            }

                            break;
                        default:
                            break;
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
    }

    private async Task<ApiException?> DeviceNoahOutputValueQueueWatchdog_OnItemDequeued(DeviceNoahOutputValueQuery item, GrowattApiClient growattApiClient)
    {
        var dbContext = GetDbContext();
        RealTimeMeasurementExtention? dataRealTimeMeasurementApiServiceInfo = null;
        RealTimeMeasurementExtention? dataRealTimeMeasurementDbContext = null;
        Device? device = null;

        if (item != null && !string.IsNullOrWhiteSpace(item.DeviceType) && !string.IsNullOrWhiteSpace(item.DeviceSn))
        {
            device = ApiServiceInfo.Devices.FirstOrDefault(x => x.DeviceSn == item.DeviceSn);
            if (item.TS.HasValue)
            {
                dataRealTimeMeasurementApiServiceInfo = ApiServiceInfo.RealTimeMeasurement.FirstOrDefault(x => x.TS == item.TS);
                dataRealTimeMeasurementDbContext = dbContext.RealTimeMeasurements.FirstOrDefault(x => x.TS == item.TS);
            }

            var entry = new ApiCallLog()
            {
                MethodeName = DeviceNoahLastDataQuery.QueryTypes.SetPowerAsync,
                TimeStamp = DateTime.Now,
                RaisedError = false
            };

            try
            {
                //Add log entry
                ApiServiceInfo.DataReads.Add(entry);

                await growattApiClient.SetPowerAsync(item.DeviceSn, "noah", item.Value);
                Logger.LogDebug($"Sended Methode: SetPowerAsync, Device: {item.DeviceSn}, {item.Value} W");

                if (device != null)
                    lock (ApiServiceInfo.Devices._syncRoot)
                        device.PowerValueCommited = item.Value;

                if (dataRealTimeMeasurementApiServiceInfo != null)
                    lock (ApiServiceInfo.RealTimeMeasurement._syncRoot)
                        dataRealTimeMeasurementApiServiceInfo.CommitedPowerValue = item.Value;

                if (dataRealTimeMeasurementDbContext != null)
                {
                    dataRealTimeMeasurementDbContext.CommitedPowerValue = item.Value;
                    await dbContext.SaveChangesAsync();
                }

                Logger.LogTrace($"Commited lastRequestedPowerValue: {item.Value} W");

                ApiServiceInfo.InvokeStateHasChanged();

                return default; // Operation erfolgreich
            }
            catch (ApiException ex)
            {
                return ex; // Operation fehlgeschlagen
            }
        }
        return default;
    }

    private async Task<ApiException?> DeviceNoahTimeSegmentQueueWatchdog_OnItemDequeued(DeviceNoahTimeSegmentQuery item, GrowattApiClient growattApiClient)
    {
        if (item != null && !string.IsNullOrWhiteSpace(item.DeviceType) && !string.IsNullOrWhiteSpace(item.DeviceSn))
        {
            try
            {
                var entry = new ApiCallLog()
                {
                    MethodeName = DeviceNoahLastDataQuery.QueryTypes.DeviceNoahTimeSegment,
                    TimeStamp = DateTime.Now,
                    RaisedError = false
                };

                //if (_demoMode) return default;

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

            DeviceNoahLastDataQueueWatchdog.Enqueue(new DeviceNoahLastDataQuery()
            {
                QueryType = DeviceNoahLastDataQuery.QueryTypes.DeviceNoahInfo,
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

            DeviceNoahLastDataQueueWatchdog.Enqueue(new DeviceNoahLastDataQuery()
            {
                QueryType = DeviceNoahLastDataQuery.QueryTypes.DeviceNoahLastData,
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
        if (ApiServiceInfo.DataReadsDoRefresh(DeviceNoahLastDataQuery.QueryTypes.DeviceNoahInfo))
        {
            GetDeviceNoahInfo();
        }

        //Refresh Last data ever minute
        if (ApiServiceInfo.DataReadsDoRefresh(DeviceNoahLastDataQuery.QueryTypes.DeviceNoahLastData))
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
