using EnergyAutomate;
using EnergyAutomate.Data;
using EnergyAutomate.Definitions;
using EnergyAutomate.Watchdogs;
using Growatt.OSS;
using Growatt.Sdk;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Tibber.Sdk;

public partial class ApiService : IObserver<RealTimeMeasurement>, IDisposable
{
    #region Timer

    private Timer Timer { get; init; }

    #region Properties

    private int timerDelay = 600;
    private bool timerIsOffline = false;
    private int timerPenalty = 0;
    private bool timmerIsRunning = false;
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

    #endregion Properties

    private void CheckOfflineCondition()
    {
        timerIsOffline = ApiServiceInfo.Devices.Any(x => x.DeviceType == "noah" && x.IsOfflineSince != null);
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

    private async void TimerCallback(object? state)
    {
        if (timmerIsRunning) return;

        if (timerIsOffline)
        {
            LoadLastData();
            CheckOfflineCondition();
            return;
        }

        timmerIsRunning = true;

        try
        {
            var ApiServiceInfo = ServiceProvider.GetRequiredService<ApiServiceInfo>();
            var client = ServiceProvider.GetRequiredService<GrowattApiClient>();

            var lastRequestedPowerValueItem = ApiServiceInfo.LastRequestedPowerValueItem;
            var lastCommitedPowerValueItem = ApiServiceInfo.LastCommitedPowerValueItem;

            if
            (
                (lastCommitedPowerValueItem == null && lastRequestedPowerValueItem != null) || //no last commit but requested
                (
                    lastCommitedPowerValueItem != null && lastRequestedPowerValueItem != null && //exits last commit and last requested
                    lastCommitedPowerValueItem.TS < lastRequestedPowerValueItem.TS && //not the same item
                    lastCommitedPowerValueItem.RequestedPowerValue != lastRequestedPowerValueItem.RequestedPowerValue //Not the same value
                )
            )
            {
                if (lastRequestedPowerValueItem.RequestedPowerValue.HasValue && !lastRequestedPowerValueItem.CommitedPowerValue.HasValue) //double check if not already commited
                {
                    var dbContext = ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var newPowerValue = lastRequestedPowerValueItem.RequestedPowerValue.Value / 2;

                    bool exitLoops = false;
                    foreach (var device in ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah"))
                    {
                        bool isCommited = false;
                        while (!isCommited)
                        {
                            try
                            {
                                Logger.LogDebug($"Sending Methode: TimerCallback.SetPowerAsync, Device: {device.DeviceSn}, {newPowerValue} W");
                                await client.SetPowerAsync(device.DeviceSn, "noah", (int)(newPowerValue));
                                Logger.LogDebug($"Sending Methode: TimerCallback.SetPowerAsync, Device: {device.DeviceSn}, {newPowerValue} W");

                                isCommited = true;

                                if (timerPenalty >= 100) timerPenalty -= 100;
                            }
                            catch (ApiException ex)
                            {
                                if (ex.ErrorCode == 5)
                                {
                                    GetDeviceNoahInfo();
                                    Logger.LogError($"Device {device.DeviceSn} is offline");
                                    Logger.LogError(ex, ex.Message);
                                    isCommited = true;
                                    exitLoops = true;
                                    break;
                                }
                                else
                                {
                                    Logger.LogError(ex, $"Error Methode: TimerCallback.SetPowerAsync, Device: {device.DeviceSn}, {newPowerValue} W");
                                    Logger.LogError(ex, ex.Message);
                                    timerPenalty += 100;
                                }
                            }
                            await Task.Delay(ApiServiceInfo.SettingLockSeconds);
                        }
                        if (exitLoops) break;
                    }

                    var data = dbContext.RealTimeMeasurements.FirstOrDefault(x => x.TS == lastRequestedPowerValueItem.TS);
                    if (data != null)
                    {
                        Logger.LogDebug($"Commit lastRequestedPowerValue: {lastRequestedPowerValueItem.RequestedPowerValue.Value} W");
                        data.CommitedPowerValue = lastRequestedPowerValueItem.RequestedPowerValue.Value;
                        await dbContext.SaveChangesAsync();
                        Logger.LogDebug($"Commited lastRequestedPowerValue: {lastRequestedPowerValueItem.RequestedPowerValue.Value} W");
                    }
                }
            }
        }
        catch (Exception)
        {
            throw;
        }

        timmerIsRunning = false;
    }

    #endregion Timer

    #region IObserver

    private ApplicationDbContext DbContext => ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();

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
        Console.WriteLine($"{value.Timestamp} - consumtion: {value.Power:N0} W production: {value.PowerProduction:N0} W; ");

        var newValue = new RealTimeMeasurementExtention(value);

        await RealTimeMeasurement(newValue);

        await Write2Database(newValue);
    }

    private async Task Write2Database(RealTimeMeasurementExtention value)
    {
        Logger.LogDebug($"RequestedPowerValue: {value.RequestedPowerValue}");

        value.SettingLockSeconds = ApiServiceInfo.SettingLockSeconds;
        value.SettingPowerLoadSeconds = ApiServiceInfo.SettingPowerLoadSeconds;
        value.AvgPowerLoad = ApiServiceInfo.AvgPowerLoad;
        value.SettingOffSetAvg = ApiServiceInfo.SettingOffsetAvg;
        value.SettingToleranceAvg = ApiServiceInfo.SettingToleranceAvg;

        ApiServiceInfo.RealTimeMeasurement.Add(value);
        DbContext.RealTimeMeasurements.Add(value); // Speichern in der Datenbank

        await DbContext.SaveChangesAsync(); // Änderungen speichern
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
        await SetNoDeviceNoahTimeSegments();
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
    public ApiQueueWatchdog<DeviceNoahLastDataQuery> DeviceNoahLastDataQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahLastDataQuery>>();
    public ApiQueueWatchdog<DeviceNoahTimeSegmentQuery> DeviceNoahTimeSegmentQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahTimeSegmentQuery>>();
    private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog { get; set; }
    private IConfiguration Configuration => ServiceProvider.GetRequiredService<IConfiguration>();
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
        var client = ServiceProvider.GetRequiredService<GrowattApiClient>();
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

    private void CalcNewOutputValue(RealTimeMeasurementExtention value)
    {
        int lastCommitedPowerValue = ApiServiceInfo.LastCommitedPowerValue == null ? ApiServiceInfo.GetNoahCurrentPowerValueSum() : ApiServiceInfo.LastCommitedPowerValue ?? 0;

        ApiServiceInfo.DifferencePowerValue = Math.Abs(ApiServiceInfo.SettingOffsetAvg - ApiServiceInfo.AvgPowerLoad);

        ApiServiceInfo.DeltaPowerValue = ApiServiceInfo.DifferencePowerValue switch
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

        var lastRequestedPowerValue = ApiServiceInfo.LastRequestedPowerValue ?? lastCommitedPowerValue;

        if (ApiServiceInfo.AvgPowerLoad > ApiServiceInfo.SettingOffsetAvg + (ApiServiceInfo.SettingToleranceAvg / 2))
        {
            ApiServiceInfo.NewPowerValue = lastRequestedPowerValue + ApiServiceInfo.DeltaPowerValue;
        }
        else if (ApiServiceInfo.AvgPowerLoad < ApiServiceInfo.SettingOffsetAvg - (ApiServiceInfo.SettingToleranceAvg / 2))
        {
            ApiServiceInfo.NewPowerValue = lastRequestedPowerValue - ApiServiceInfo.DeltaPowerValue;
        }

        var maxPower = ApiServiceInfo.SettingMaxPower;

        ApiServiceInfo.NewPowerValue = ApiServiceInfo.NewPowerValue > maxPower ? maxPower : ApiServiceInfo.NewPowerValue < 0 ? 0 : ApiServiceInfo.NewPowerValue;

        if (ApiServiceInfo.NewPowerValue <= maxPower)
        {
            if (ApiServiceInfo.NewPowerValue != ApiServiceInfo.LastRequestedPowerValue)
            {
                value.RequestedPowerValue = ApiServiceInfo.NewPowerValue;
            }
        }
        else
        {
            Debug.WriteLine($"PowerChanged: {ApiServiceInfo.LastRequestedPowerValue ?? 0} >> {ApiServiceInfo.NewPowerValue}, OffSet: {ApiServiceInfo.SettingOffsetAvg}");
        }

        ApiServiceInfo.InvokeStateHasChanged();
    }

    private async Task DisableAutoMode(RealTimeMeasurementExtention value)
    {
        if (ApiServiceInfo.SettingLoadBalanced)
        {
            await SetLoadPriorityDeviceNoahTimeSegments();
        }
    }

    private async Task EnableAutoMode(RealTimeMeasurementExtention value)
    {
        GetDeviceNoahLastData();

        await Task.Delay(2000);

        await SetNoDeviceNoahTimeSegments();
    }

    private async Task RealTimeMeasurement(RealTimeMeasurementExtention value)
    {
        ApiServiceInfo.AvgPowerLoad = CalcAvgOfLastSeconds(ApiServiceInfo.SettingPowerLoadSeconds);

        if (ApiServiceInfo.SettingAutoMode)
        {
            if (!isRealTimeMeasurementRunning)
            {
                await EnableAutoMode(value);
            }

            if (ApiServiceInfo.SettingAutoModeRestriction)
            {
                if (ApiServiceInfo.GetCurrentAutoModeRestriction())
                {
                    CalcNewOutputValue(value);
                }
                else
                {
                }
            }
            else
            {
                CalcNewOutputValue(value);
            }

            isRealTimeMeasurementRunning = true;
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

    #endregion Tibber

    #region Growatt

    #region Configs

    private Task ClearDeviceNoahTimeSegments(Queue<DeviceNoahTimeSegmentQuery> DeviceTimeSegmentQueue)
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
                        EndTime = "0:0",
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

    private async Task SetLoadPriorityDeviceNoahTimeSegments()
    {
        Queue<DeviceNoahTimeSegmentQuery> DeviceTimeSegmentQueue = new();

        await ClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

        var deviceSnList = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

        foreach (var deviceSn in deviceSnList)
        {
            DeviceTimeSegmentQueue.Enqueue(new DeviceNoahTimeSegmentQuery()
            {
                Force = true,
                DeviceSn = deviceSn,
                DeviceType = "noah",
                Type = "0",
                StartTime = "0:0",
                EndTime = "0:0",
                Mode = "0",
                Power = "0",
                Enable = "0"
            });
        }

        DeviceNoahTimeSegmentQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
    }

    private async Task SetNoDeviceNoahTimeSegments()
    {
        Queue<DeviceNoahTimeSegmentQuery> DeviceTimeSegmentQueue = new();

        await ClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

        DeviceNoahTimeSegmentQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
    }

    #endregion Configs

    #region Functions

    private async Task<ApiException?> DeviceNoahLastDataQueueWatchdog_OnItemDequeued(DeviceNoahLastDataQuery item, GrowattApiClient client)
    {
        try
        {
            var entry = new ApiCallLog()
            {
                MethodeName = item.QueryType,
                TimeStamp = DateTime.Now,
                RaisedError = false
            };

            switch (item.QueryType)
            {
                case "DeviceNoahLastData":
                    var deviceNoahLastDatas = await client.GetDeviceLastDataAsync(item.DeviceType, item.DeviceSn);
                    if (deviceNoahLastDatas != null)
                    {
                        await SaveDeviceNoahLastData(deviceNoahLastDatas);
                    }
                    break;
                case "DeviceNoahInfo":
                    var deviceNoahInfos = await client.GetDeviceInfoAsync(item.DeviceType, item.DeviceSn);
                    if (deviceNoahInfos != null)
                    {
                        await SaveDeviceNoahInfo(deviceNoahInfos);
                    }
                    break;
                default:
                    break;
            }

            ApiServiceInfo.DataReads.Add(entry);

            return default; // Operation erfolgreich
        }
        catch (ApiException ex)
        {
            return ex; // Operation fehlgeschlagen
        }
    }

    private async Task<ApiException?> DeviceNoahTimeSegmentQueueWatchdog_OnItemDequeued(DeviceNoahTimeSegmentQuery item, GrowattApiClient client)
    {
        try
        {
            await client.SetTimeSegmentAsync(item);
            return default; // Operation erfolgreich
        }
        catch (ApiException ex)
        {
            return ex; // Operation fehlgeschlagen
        }
    }

    #endregion Functions

    #region Basics

    private GrowattApiClient GrowattApiClient { get; set; }

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
                QueryType = "DeviceNoahInfo",
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
                QueryType = "DeviceNoahLastData",
                DeviceType = "noah",
                DeviceSn = deviceSnList,
            });
        }
    }

    private void LoadLastData()
    {
        if (ApiServiceInfo.DataReads.Any(x => x.MethodeName == "GetDeviceLastDataAsync" && x.TimeStamp > DateTime.Now.AddMinutes(-1)))
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
    }

    #endregion Basics

    #endregion Growatt

    #endregion Methodes
}
