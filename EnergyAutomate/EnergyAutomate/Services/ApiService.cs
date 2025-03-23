using EnergyAutomate;
using EnergyAutomate.Data;
using EnergyAutomate.Definitions;
using EnergyAutomate.Watchdogs;
using Growatt.OSS;
using Growatt.Sdk;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using Tibber.Sdk;

public partial class ApiService : IObserver<RealTimeMeasurement>, IDisposable
{
    #region Timer

    private Timer Timer { get; init; }

    #region Properties

    private bool timmerIsRunning = false;
    private bool timerIsOffline = false;
    private int timerDelay = 600;
    private int timerPenalty = 0;

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

    #endregion

    private async void TimerCallback(object? state)
    {
        if (timmerIsRunning) return;

        if (timerIsOffline)
        {
            await LoadDeviceLastData();
            CheckOfflineCondition();
            return;
        }

        timmerIsRunning = true;

        var ApiServiceInfo = ServiceProvider.GetRequiredService<ApiServiceInfo>();

        var configuration = ServiceProvider.GetRequiredService<IConfiguration>();
        var client = new GrowattApiClient("https://openapi.growatt.com", configuration["ApiSettings:GrowattApiToken"] ?? string.Empty);

        var lastRequestedPowerValueItem = ApiServiceInfo.RealTimeMeasurement.Where(x => x.RequestedPowerValue != null && x.CommitedPowerValue == null).OrderByDescending(x => x.Timestamp).FirstOrDefault();
        var lastCommitedPowerValueItem = ApiServiceInfo.RealTimeMeasurement.Where(x => x.RequestedPowerValue != null && x.CommitedPowerValue != null).OrderByDescending(x => x.Timestamp).FirstOrDefault();

        if
        (
            (lastCommitedPowerValueItem == null && lastRequestedPowerValueItem != null) ||
            (lastCommitedPowerValueItem != null && lastRequestedPowerValueItem != null && lastCommitedPowerValueItem.TS < lastRequestedPowerValueItem.TS)
        )
        {
            if (lastRequestedPowerValueItem.RequestedPowerValue.HasValue && !lastRequestedPowerValueItem.CommitedPowerValue.HasValue)
            {
                var dbContext = ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var lastRequestedPowerValue = lastRequestedPowerValueItem.RequestedPowerValue.Value;

                bool exitLoops = false;
                foreach (IDevice device in ApiServiceInfo.Devices)
                {
                    bool isCommited = false;
                    while (!isCommited)
                    {
                        try
                        {
                            await client.SetPowerAsync(device.DeviceSn, "noah", lastRequestedPowerValue);

                            await SetDeviceIsOfflineSince(device, null);

                            Logger.LogDebug($"Sended {device.DeviceSn}, {lastRequestedPowerValue} W");
                            isCommited = true;

                            if (timerPenalty >= 100) timerPenalty -= 100;
                        }
                        catch (ApiException ex)
                        {
                            if (ex.ErrorCode == 5)
                            {
                                await SetDeviceIsOfflineSince(device, DateTime.Now);

                                Logger.LogError($"Device {device.DeviceSn} is offline");
                                isCommited = true;
                                exitLoops = true;
                                break;
                            }
                            else
                            {
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
                    data.CommitedPowerValue = lastRequestedPowerValue;
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        timmerIsRunning = false;
    }

    public async Task SetDeviceIsOfflineSince(IDevice device, DateTime? isOfflineSince)
    {
        var isOfflineNull = IsOfflineNull([device]);

        if (isOfflineNull && isOfflineSince != null) return;

        var dbContext = GetDbContext();

        var itemDbContextDevices = dbContext.Devices.FirstOrDefault(x => x.DeviceSn == device.DeviceSn);
        var itemApiServiceInfoDevices = ApiServiceInfo.Devices.FirstOrDefault(x => x.DeviceSn == device.DeviceSn);
        if (itemDbContextDevices != null && itemApiServiceInfoDevices != null)
        {
            itemApiServiceInfoDevices.IsOfflineSince = isOfflineSince;
            itemDbContextDevices.IsOfflineSince = isOfflineSince;
            await dbContext.SaveChangesAsync();
        }
    }

    private bool IsOfflineNull(List<IDevice> devices)
    {
        foreach(var device in devices)
        {
            var itemApiServiceInfoDevices = ApiServiceInfo.Devices.FirstOrDefault(x => x.DeviceSn == device.DeviceSn);
            if(itemApiServiceInfoDevices?.IsOfflineSince != null) return false;
        }
        return true;
    }

    private void CheckOfflineCondition()
    {
        timerIsOffline = ApiServiceInfo.Devices.Any(x => x.DeviceType == "noah" && x.IsOfflineSince != null);
    }

    #endregion Fields

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

    #endregion

    #region Ctor

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

        DeviceNoahTimeSegmentQueueWatchdog.OnItemDequeued += async (item, client) =>
        {
            try
            {
                await client.SetTimeSegmentAsync(item);
                await SetDeviceIsOfflineSince(item, DateTime.Now);
                return default; // Operation erfolgreich
            }
            catch (ApiException ex)
            {

                return ex; // Operation fehlgeschlagen
            }
        };

        DeviceNoahLastDataQueueWatchdog.OnItemDequeued += async (item, client) =>
        {
            try
            {
                var deviceNoahLastDatas = await client.GetDeviceLastDataAsync(item.DeviceType, item.DeviceSn);
                if (deviceNoahLastDatas != null)
                {
                    await SaveDeviceNoahLastData(deviceNoahLastDatas);
                }
                return default; // Operation erfolgreich
            }
            catch (ApiException ex)
            {
                return ex; // Operation fehlgeschlagen
            }
        };
    }

    private bool isDisposed = false;

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

    private async Task RefreshDeviceList(object sender, EventArgs e)
    {
        await GetDevice();
        await RefreshDeviceNoahs(sender, e);
    }

    private async Task RefreshDeviceNoahs(object sender, EventArgs e)
    {
        var deviceSnList = string.Join(",", ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());
        await GetDeviceNoahInfo("noah", deviceSnList);
        GetDeviceNoahLastData("noah", deviceSnList);
    }

    private async Task RefreshDeviceNoahLastData(object sender, EventArgs e)
    {
        var deviceSnList = string.Join(",", ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());
        GetDeviceNoahLastData("noah", deviceSnList);
    }

    private async Task ClearDeviceNoahTimeSegments(object sender, EventArgs e)
    {
        await SetNoDeviceNoahTimeSegments();
    }

    #endregion Events

    #region Properties

    private IServiceProvider ServiceProvider { get; set; }

    private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog { get; set; }

    private IConfiguration Configuration => ServiceProvider.GetRequiredService<IConfiguration>();

    private ILogger Logger => ServiceProvider.GetRequiredService<ILogger<ApiService>>();

    public ApiServiceInfo ApiServiceInfo { get; set; }

    public ApiQueueWatchdog<DeviceNoahTimeSegmentQuery> DeviceNoahTimeSegmentQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahTimeSegmentQuery>>();

    public ApiQueueWatchdog<DeviceNoahLastDataQuery> DeviceNoahLastDataQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahLastDataQuery>>();

    private bool isRealTimeMeasurementRunning = false;

    #endregion Properties

    #region Methodes

    private ApplicationDbContext GetDbContext()
    {
        return ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configuration = ServiceProvider.GetRequiredService<IConfiguration>();
        GrowattApiClient = new GrowattApiClient("https://openapi.growatt.com", configuration["ApiSettings:GrowattApiToken"] ?? string.Empty);

        var tibberClient = new TibberApiClient(configuration["ApiSettings:TibberApiToken"] ?? string.Empty, new ProductInfoHeaderValue("EnergyAutomate", "1.0"));

        var basicData = await tibberClient.GetBasicData(cancellationToken);
        TibberHomeId = basicData.Data.Viewer.Homes.FirstOrDefault()?.Id;

        await GetTomorrowPrices();
        await LoadApiServiceInfoFromDatabase();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {

    }

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

    private async Task RealTimeMeasurement(RealTimeMeasurementExtention value)
    {
        ApiServiceInfo.AvgPowerLoad = CalcAvgOfLastSeconds(ApiServiceInfo.SettingPowerLoadSeconds);

        if (ApiServiceInfo.SettingAutoMode)
        {
            if (!isRealTimeMeasurementRunning)
            {
                isRealTimeMeasurementRunning = true;
                await EnableAutoMode();
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
        }
        else
        {
            if (isRealTimeMeasurementRunning)
            {
                isRealTimeMeasurementRunning = false;
                await DisableAutoMode(value.Timestamp.DateTime);
            }
        }
    }

    private void CalcNewOutputValue(RealTimeMeasurementExtention value)
    {
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

        // Prüfe, ob der aktuelle Preis über dem Tagesdurchschnitt liegt
        if (ApiServiceInfo.AvgPowerLoad > ApiServiceInfo.SettingOffsetAvg + (ApiServiceInfo.SettingToleranceAvg / 2))
        {
            ApiServiceInfo.NewPowerValue = ApiServiceInfo.LastRequestedPowerValue + ApiServiceInfo.DeltaPowerValue ?? 0;
        }
        else if (ApiServiceInfo.AvgPowerLoad < ApiServiceInfo.SettingOffsetAvg - (ApiServiceInfo.SettingToleranceAvg / 2))
        {
            ApiServiceInfo.NewPowerValue = ApiServiceInfo.LastRequestedPowerValue - ApiServiceInfo.DeltaPowerValue ?? 0;
        }

        var maxPower = ApiServiceInfo.SettingMaxPower;

        ApiServiceInfo.NewPowerValue = ApiServiceInfo.LastCommitedPowerValue ?? 0;
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

    private async Task EnableAutoMode()
    {
        await SetNoDeviceNoahTimeSegments();
    }

    private async Task DisableAutoMode(DateTime dateTime)
    {
        if (ApiServiceInfo.SettingLoadBalanced)
        {
            await SetLoadPriorityDeviceNoahTimeSegments();
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

    #endregion Tibber

    #region Growatt

    #region Configs

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

    private async Task ClearDeviceNoahTimeSegments(Queue<DeviceNoahTimeSegmentQuery> DeviceTimeSegmentQueue)
    {
        var deviceSnList = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

        await GetDeviceNoahInfo("noah", string.Join(",", deviceSnList));

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
    }



    #endregion

    #region Basics

    private GrowattApiClient GrowattApiClient { get; set; }

    private async Task LoadDeviceLastData()
    {
        if (ApiServiceInfo.DataReads.Any(x => x.MethodeName == "GetDeviceLastDataAsync" && x.TimeStamp > DateTime.Now.AddMinutes(-1)))
        {
            var entry = new ApiCallLog()
            {
                MethodeName = "GetDeviceLastDataAsync",
                TimeStamp = DateTime.Now,
                RaisedError = false
            };

            var devices = ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Cast<IDevice>().ToList();

            if (IsOfflineNull(devices))
            {
                var deviceSnList = string.Join(",", ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());

                DeviceNoahLastDataQueueWatchdog.Enqueue(new DeviceNoahLastDataQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = deviceSnList
                });

                ApiServiceInfo.DataReads.Add(entry);

            }
        }
    }

    private async Task GetDevice()
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

    private async Task GetDeviceNoahInfo(string deviceType, string deviceSn)
    {
        var dbContext = GetDbContext();

        List<DeviceNoahInfo>? deviceNoahInfos = null;

        while (deviceNoahInfos == null)
        {
            try
            {
                deviceNoahInfos = await GrowattApiClient.GetDeviceInfoAsync(deviceType, deviceSn);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
            }
            await Task.Delay(ApiServiceInfo.SettingLockSeconds);
        }

        if (deviceNoahInfos != null)
        {

            foreach (var item in deviceNoahInfos)
            {
                ApiServiceInfo.DeviceNoahInfo.Add(item);

                var existingDeviceNoahInfo = await dbContext.DeviceNoahInfo.FindAsync(item.DeviceSn);
                if (existingDeviceNoahInfo != null)
                {
                    dbContext.Entry(existingDeviceNoahInfo).CurrentValues.SetValues(item);
                }
                else
                {
                    dbContext.DeviceNoahInfo.Add(item);
                }
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private void GetDeviceNoahLastData(string deviceType, string deviceSn)
    {
        DeviceNoahLastDataQueueWatchdog.Enqueue(new DeviceNoahLastDataQuery()
        {
            DeviceType = deviceType,
            DeviceSn = deviceSn
        });
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
