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
using Tibber.Sdk;

public partial class ApiService : IObserver<RealTimeMeasurement>, IDisposable
{
    #region Timer

    private int timerDelay = 600;
    private int timerPenalty = 0;
    private Timer Timer { get; init; }
    private int TimerDelay
    {
        get
        {
            return timerDelay;
        }
        set
        {
            timerDelay = value;
            Timer.Change(0, value);
        }
    }

    private int TotalDelay => timerDelay + timerPenalty;

    private void TimerCallback(object? state)
    {
        ProceedRules();
    }

    #endregion Timer

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
        var dbContext = GetDbContext();

        var realTimeMeasurementExtention = new RealTimeMeasurementExtention(value);

        ApiServiceInfo.AvgPowerLoad = CalcAvgOfLastSeconds(ApiServiceInfo.SettingAvgPowerLoadSeconds);
        realTimeMeasurementExtention.AvgPowerLoad = ApiServiceInfo.AvgPowerLoad;

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

    #endregion IObserver

    #region Ctor

    private bool isDisposed = false;

    public ApiService(IServiceProvider serviceProvider, ApiServiceInfo apiServiceInfo, ApiRealTimeMeasurementWatchdog apiRealTimeMeasurementWatchdog)
    {
        Timer = new Timer(TimerCallback, null, 0, TimerDelay);

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

    public int CalcAvgOfLastSeconds(int sec)
    {
        var now = DateTimeOffset.Now;

        lock (ApiServiceInfo.RealTimeMeasurement._syncRoot)
        {
            var result = ApiServiceInfo.RealTimeMeasurement
                .Where(m => m.Timestamp >= now.AddSeconds(-sec))
                .ToList();

            if (!result.Any())
            {
                return 0;
            }

            var avg = result.Average(m => m.TotalPower);
            return (int)avg;
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

    // Basis-Schrittweite für die Anpassung des eingespeisten Stroms
    /// <summary>
    /// Adjusts the power output based on the difference between the current power consumption and
    /// the target power consumption. The adjustment takes into account both the magnitude and
    /// duration of the deviation to determine the adjustment step size.
    /// </summary>
    /// <param name="state">The state object passed by the Timer (not used).</param>
    private void AdjustPower1(RealTimeMeasurementExtention value)
    {
        var deviceCount = ApiServiceInfo.GetNoahDeviceCount();
        var currentPowerValueSum = ApiServiceInfo.GetNoahCurrentPowerValueSum();
        var currentConsumption = CalcAvgOfLastSeconds(ApiServiceInfo.SettingAvgPowerLoadSeconds);
        ApiServiceInfo.AvgPowerLoad = currentConsumption;

        var targetConsumption = ApiServiceInfo.SettingAvgPowerOffset;
        var difference = Math.Abs(currentConsumption - targetConsumption);

        // true if it is discharging
        var state = ApiServiceInfo.GetNoahCurrentIsDischarchingState();
        var isDischaring = state > 1;

        // Überproduktion prüfen: Wenn der Strom positiv ist und es keine Entladung gibt
        var isOverproduction = currentPowerValueSum > 0 && !isDischaring;

        // Wenn es eine Überproduktion gibt, keine Anpassung vornehmen
        if (isOverproduction)
        {
            Logger.LogTrace("Overproduction detected, no adjustment needed.");
            return;
        }

        // Wenn die Akkus entladen werden, keine Anpassung vornehmen
        if (isDischaring)
        {
            Logger.LogTrace("Batteries are in use.");
        }

        // Letzten gesetzten Power-Wert holen
        var lastCommitedPowerValue = ApiServiceInfo.GetLastCommitedPowerValue() ?? currentPowerValueSum;

        if (lastCommitedPowerValue < 0) lastCommitedPowerValue = 0;

        Logger.LogTrace($"Values >> readLastCommitedPowerValue: {ApiServiceInfo.GetLastCommitedPowerValue()} W, currentPowerValueSum: {currentPowerValueSum} W, newLastCommitedPowerValue: {lastCommitedPowerValue} W");

        // Adaptive Hysterese: Nur reagieren, wenn die Abweichung groß genug ist
        var dynamicHysteresis = ApiServiceInfo.SettingAvgPowerHysteresis * (1 + Math.Log10(1 + difference));

        Logger.LogTrace($"CalcPower >> CurrentConsumption: {currentConsumption} W, Offset: {ApiServiceInfo.SettingAvgPowerOffset}, Diff: {difference}, Hysteresis: {dynamicHysteresis}");
        if (difference > dynamicHysteresis)
        {
            var timeSinceLastAdjustment = (DateTime.Now - ApiServiceInfo.SettingAvgPowerlastAdjustmentTime).TotalSeconds;

            // Logarithmische Anpassung für sanftes Nachregeln
            var adjustmentFactor = Math.Log10(difference + 1) * (timeSinceLastAdjustment / 10);
            adjustmentFactor = Math.Clamp(adjustmentFactor, 0.1, 10); // Begrenzung für Stabilität

            // Berechnung des neuen Werts basierend auf dem letzten Wert
            var adjustmentStep = (int)(ApiServiceInfo.SettingAvgPowerAdjustmentStep * adjustmentFactor);
            int newPowerValue = lastCommitedPowerValue + (difference > 0 ? -adjustmentStep : adjustmentStep);
            Logger.LogTrace($"AdjustPower >> TimeSinceLastAdjustment: {timeSinceLastAdjustment} sec, adjustmentFactor: {adjustmentFactor}, adjustmentStep: {adjustmentStep}, Hysteresis: {dynamicHysteresis}");

            // Begrenzung auf max. zulässige Leistung mit Logging
            if (newPowerValue > ApiServiceInfo.SettingMaxPower)
            {
                Logger.LogTrace($"MaxPower override: {newPowerValue} >> {ApiServiceInfo.SettingMaxPower} W");
                newPowerValue = ApiServiceInfo.SettingMaxPower;
            }

            // Sicherstellen, dass der neue Power-Wert durch die Anzahl der Geräte teilbar ist
            if (deviceCount > 0)
            {
                int remainder = newPowerValue % deviceCount;
                if (remainder != 0)
                {
                    // Falls Rest vorhanden, anpassen
                    newPowerValue -= remainder;
                }
            }

            // Setzen den neuen angeforderten Wertes falls er sich geändert hat
            if (newPowerValue != ApiServiceInfo.GetLastRequestedPowerValue())
            {
                value.RequestedPowerValue = newPowerValue;
                ApiServiceInfo.NewPowerValue = newPowerValue;
                Logger.LogTrace($"PowerChanged >> NewPowerValue: {newPowerValue} W, Offset: {ApiServiceInfo.SettingAvgPowerOffset}, Diff: {difference}, Hysteresis: {dynamicHysteresis}");

                var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").ToList();
                var deviceSnList = string.Join(",", devices.Select(x => x.DeviceSn).ToList());
                DeviceNoahOutputValueQueueWatchdog.Enqueue(new DeviceNoahOutputValueQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = deviceSnList,
                    Value = newPowerValue,
                    TS = value.TS
                });
            }

            // Update letzter Anpassungszeitpunkt
            ApiServiceInfo.SettingAvgPowerlastAdjustmentTime = DateTime.Now;
            ApiServiceInfo.SettingAvgPowerLastDifference = difference;

            ApiServiceInfo.InvokeStateHasChanged();
        }
        else
        {
            Logger.LogTrace($"AdjustPower: {difference} < {dynamicHysteresis} Nothing to do");
        }
    }

    private void AdjustPower2(RealTimeMeasurementExtention value)
    {
        int lastCommitedPowerValue = ApiServiceInfo.GetLastCommitedPowerValueItem() == null ? ApiServiceInfo.GetNoahCurrentPowerValueSum() : ApiServiceInfo.GetLastCommitedPowerValue() ?? 0;

        ApiServiceInfo.SettingAvgPowerLastDifference = Math.Abs(ApiServiceInfo.SettingAvgPowerOffset - ApiServiceInfo.AvgPowerLoad);

        ApiServiceInfo.DeltaPowerValue = ApiServiceInfo.SettingAvgPowerLastDifference switch
        {
            > 300 => 150,
            > 250 => 125,
            > 200 => 100,
            > 150 => 75,
            > 100 => 50,
            > 75 => 35,
            > 50 => 25,
            > 25 => 10,
            > 10 => 5,
            _ => 0
        };

        var lastRequestedPowerValue = ApiServiceInfo.GetLastRequestedPowerValue() ?? lastCommitedPowerValue;

        if (ApiServiceInfo.AvgPowerLoad > ApiServiceInfo.SettingAvgPowerOffset + (ApiServiceInfo.SettingAvgPowerHysteresis / 2))
        {
            ApiServiceInfo.NewPowerValue = lastRequestedPowerValue + ApiServiceInfo.DeltaPowerValue;
        }
        else if (ApiServiceInfo.AvgPowerLoad < ApiServiceInfo.SettingAvgPowerOffset - (ApiServiceInfo.SettingAvgPowerHysteresis / 2))
        {
            ApiServiceInfo.NewPowerValue = lastRequestedPowerValue - ApiServiceInfo.DeltaPowerValue;
        }

        var maxPower = ApiServiceInfo.SettingMaxPower;

        ApiServiceInfo.NewPowerValue = ApiServiceInfo.NewPowerValue > maxPower ? maxPower : ApiServiceInfo.NewPowerValue < 0 ? 0 : ApiServiceInfo.NewPowerValue;

        if (ApiServiceInfo.NewPowerValue <= maxPower)
        {
            if (ApiServiceInfo.NewPowerValue != ApiServiceInfo.GetLastRequestedPowerValue())
            {
                var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").ToList();
                var deviceSnList = string.Join(",", devices.Select(x => x.DeviceSn).ToList());
                DeviceNoahOutputValueQueueWatchdog.Enqueue(new DeviceNoahOutputValueQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = deviceSnList,
                    Value = ApiServiceInfo.NewPowerValue,
                    TS = value.TS
                });
                value.RequestedPowerValue = ApiServiceInfo.NewPowerValue;
            }
        }
        else
        {
            Debug.WriteLine($"PowerChanged: {ApiServiceInfo.GetLastRequestedPowerValue() ?? 0} >> {ApiServiceInfo.NewPowerValue}, OffSet: {ApiServiceInfo.SettingAvgPowerOffset}");
        }

        ApiServiceInfo.InvokeStateHasChanged();
    }

    private void CalcNewOutputValue(RealTimeMeasurementExtention value)
    {
        //int lastCommitedPowerValue = ApiServiceInfo.GetLastRequestedPowerValueItem() == null ? ApiServiceInfo.GetNoahCurrentPowerValueSum() : ApiServiceInfo.GetLastCommitedPowerValue() ?? 0;

        //ApiServiceInfo.DifferencePowerValue = Math.Abs(ApiServiceInfo.SettingOffsetAvg - ApiServiceInfo.AvgPowerLoad);

        //ApiServiceInfo.DeltaPowerValue = ApiServiceInfo.DifferencePowerValue switch
        //{
        //    > 300 => 150,
        //    > 250 => 125,
        //    > 200 => 100,
        //    > 150 => 75,
        //    > 100 => 50,
        //    > 75 => 35,
        //    > 50 => 25,
        //    > 25 => 10,
        //    > 10 => 5,
        //    _ => 0
        //};

        //var lastRequestedPowerValue = ApiServiceInfo.GetLastRequestedPowerValue() ?? lastCommitedPowerValue;

        //if (ApiServiceInfo.AvgPowerLoad > ApiServiceInfo.SettingOffsetAvg + (ApiServiceInfo.SettingToleranceAvg / 2))
        //{
        //    ApiServiceInfo.NewPowerValue = lastRequestedPowerValue + ApiServiceInfo.DeltaPowerValue;
        //}
        //else if (ApiServiceInfo.AvgPowerLoad < ApiServiceInfo.SettingOffsetAvg - (ApiServiceInfo.SettingToleranceAvg / 2))
        //{
        //    ApiServiceInfo.NewPowerValue = lastRequestedPowerValue - ApiServiceInfo.DeltaPowerValue;
        //}

        //var maxPower = ApiServiceInfo.SettingMaxPower;

        //ApiServiceInfo.NewPowerValue = ApiServiceInfo.NewPowerValue > maxPower ? maxPower : ApiServiceInfo.NewPowerValue < 0 ? 0 : ApiServiceInfo.NewPowerValue;

        //if (ApiServiceInfo.NewPowerValue <= maxPower)
        //{
        //    if (ApiServiceInfo.NewPowerValue != ApiServiceInfo.GetLastRequestedPowerValue())
        //    {
        //        value.RequestedPowerValue = ApiServiceInfo.NewPowerValue;
        //    }
        //}
        //else
        //{
        //    Debug.WriteLine($"PowerChanged: {ApiServiceInfo.GetLastRequestedPowerValue() ?? 0} >> {ApiServiceInfo.NewPowerValue}, OffSet: {ApiServiceInfo.SettingOffsetAvg}");
        //}

        //ApiServiceInfo.InvokeStateHasChanged();
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

        await ClearPowerSetAsync();
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
                        //AdjustPower1(value);
                        AdjustPower2(value);
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
                    //AdjustPower1(value);
                    AdjustPower2(value);
                }
            }
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

    private Task ClearPowerSetAsync()
    {
        var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").ToList();
        var deviceSnList = string.Join(",", devices.Select(x => x.DeviceSn).ToList());
        DeviceNoahOutputValueQueueWatchdog.Enqueue(new DeviceNoahOutputValueQuery()
        {
            DeviceType = "noah",
            DeviceSn = deviceSnList,
            Value = 0,
            Force = true
        });

        return Task.CompletedTask;
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
                StartTime = "00:00",
                EndTime = "23:59",
                Mode = "0",
                Power = "0",
                Enable = "1"
            });
        }

        DeviceNoahTimeSegmentQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
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
                StartTime = "00:00",
                EndTime = "23:59",
                Mode = "1",
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

                //Add log entry
                ApiServiceInfo.DataReads.Add(entry);

                switch (item.QueryType)
                {
                    case DeviceNoahLastDataQuery.QueryTypes.DeviceNoahLastData:
                        var deviceNoahLastDatas = await growattApiClient.GetDeviceLastDataAsync(item.DeviceType, item.DeviceSn);
                        if (deviceNoahLastDatas != null)
                        {
                            await SaveDeviceNoahLastData(deviceNoahLastDatas);
                        }
                        break;
                    case DeviceNoahLastDataQuery.QueryTypes.DeviceNoahInfo:
                        var deviceNoahInfos = await growattApiClient.GetDeviceInfoAsync(item.DeviceType, item.DeviceSn);
                        if (deviceNoahInfos != null)
                        {
                            await SaveDeviceNoahInfo(deviceNoahInfos);
                        }
                        break;
                    default:
                        break;
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

        if (item != null && !string.IsNullOrWhiteSpace(item.DeviceType) && !string.IsNullOrWhiteSpace(item.DeviceSn))
        {
            try
            {
                bool exitLoops = false;

                var devices = item.DeviceSn.Split(",");
                var deviceCount = devices.Length;
                var newPowerValue = item.Value / deviceCount;

                foreach (var deviceSn in devices)
                {
                    bool isCommited = false;
                    while (!isCommited)
                    {
                        try
                        {
                            await growattApiClient.SetPowerAsync(deviceSn, "noah", (int)(newPowerValue));
                            Logger.LogDebug($"Sended Methode: TimerCallback.SetPowerAsync, Device: {deviceSn}, {newPowerValue} W");

                            isCommited = true;

                            if (timerPenalty >= 100) timerPenalty -= 100;
                        }
                        catch (ApiException ex)
                        {
                            if (ex.ErrorCode == 5)
                            {
                                DeviceNoahIsOffline = true;
                                throw;
                            }
                            else
                                timerPenalty += 100;
                        }
                        await Task.Delay(ApiServiceInfo.SettingLockSeconds);
                    }
                    if (exitLoops) break;
                }

                if (item.TS.HasValue)
                {
                    lock (ApiServiceInfo.RealTimeMeasurement._syncRoot)
                    {
                        var dataRealTimeMeasurement = ApiServiceInfo.RealTimeMeasurement.FirstOrDefault(x => x.TS == item.TS);
                        if (dataRealTimeMeasurement != null)
                            dataRealTimeMeasurement.CommitedPowerValue = item.Value;
                    }

                    var dataDbContext = dbContext.RealTimeMeasurements.FirstOrDefault(x => x.TS == item.TS);
                    if (dataDbContext != null)
                    {
                        dataDbContext.CommitedPowerValue = item.Value;
                        await dbContext.SaveChangesAsync();
                    }

                    Logger.LogTrace($"Commited lastRequestedPowerValue: {item.Value} W");
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
