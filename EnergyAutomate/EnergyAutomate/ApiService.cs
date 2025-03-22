using Azure.Core;
using EnergyAutomate;
using EnergyAutomate.Data;
using Growatt.OSS;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using Tibber.Sdk;

public partial class ApiService : IObserver<RealTimeMeasurement>, IDisposable
{
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

        await RealTimeMeasurement(value);

        await Write2Database(value);
    }

    private async Task Write2Database(RealTimeMeasurement value)
    {
        var newRealTimeMeasurementExtention = new RealTimeMeasurementExtention(value)
        {
            TS = value.Timestamp.DateTime,
            DeviceInfos = [.. ApiServiceInfo.LastOutputValues],
            SettingLockSeconds = ApiServiceInfo.SettingLockSeconds,
            SettingPowerLoadSeconds = ApiServiceInfo.SettingPowerLoadSeconds,
            AvgPowerLoad = ApiServiceInfo.AvgPowerLoad,
            SettingOffSetAvg = ApiServiceInfo.SettingOffsetAvg,
            SettingToleranceAvg = ApiServiceInfo.SettingToleranceAvg
        };
        ApiServiceInfo.RealTimeMeasurement.Add(newRealTimeMeasurementExtention);
        DbContext.RealTimeMeasurements.Add(newRealTimeMeasurementExtention); // Speichern in der Datenbank

        await DbContext.SaveChangesAsync(); // Änderungen speichern
    }

    #endregion

    #region Ctor

    public ApiService(IServiceProvider serviceProvider, ApiServiceInfo apiServiceInfo, ApiRealTimeMeasurementWatchdog apiRealTimeMeasurementWatchdog)
    {
        ServiceProvider = serviceProvider;
        ApiServiceInfo = apiServiceInfo;
        ApiRealTimeMeasurementWatchdog = apiRealTimeMeasurementWatchdog;

        ApiServiceInfo.RefreshDeviceList += RefreshDeviceList;
        ApiServiceInfo.RefreshNoahs += RefreshDeviceNoahs;
        ApiServiceInfo.RefreshNoahLastData += RefreshDeviceNoahLastData;
        ApiServiceInfo.ClearDeviceNoahTimeSegments += SetNoTimerDeviceNoahs;
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

    #region Fields

    private bool isRealTimeMeasurementRunning = false;
    private bool isGrowattValueChangeQueueRunning = false;
    private int penaltyFrequentlyAccess = 0;

    #endregion Fields

    #region Properties

    private IServiceProvider ServiceProvider { get; set; }

    private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog { get; set; }

    private IConfiguration Configuration => ServiceProvider.GetRequiredService<IConfiguration>();

    private ILogger Logger => ServiceProvider.GetRequiredService<ILogger<ApiService>>();

    public ApiServiceInfo ApiServiceInfo { get; set; }

    #endregion Properties

    #region Methodes

    private ApplicationDbContext GetDbContext()
    {
        return ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = Configuration["ApiSettings:GrowattApiToken"];
        GrowattApiClient = new GrowattApiClient("https://openapi.growatt.com", token ?? string.Empty);

        var basicData = await TibberApiClient.GetBasicData(cancellationToken);
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

    private TibberApiClient TibberApiClient => new TibberApiClient(Configuration["ApiSettings:TibberApiToken"] ?? string.Empty, new ProductInfoHeaderValue("EnergyAutomate", "1.0"));

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

    private async Task RealTimeMeasurement(RealTimeMeasurement value)
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
                if (ApiServiceInfo.GetStartHoure() > value.Timestamp.Hour || value.Timestamp.Hour < 8)
                {
                    await CalcNewOutputValue(value);
                }
                else
                {

                }
            }
            else
            {
                await CalcNewOutputValue(value);
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

    private async Task CalcNewOutputValue(RealTimeMeasurement value)
    {
        await LoadDeviceData();

        List<ApiOutputValueDeviceInfo> lastOutputValue = [];

        ApiServiceInfo.DifferencePowerValue = Math.Abs(ApiServiceInfo.SettingOffsetAvg - ApiServiceInfo.AvgPowerLoad);

        ApiServiceInfo.DeltaPowerValue = ApiServiceInfo.DifferencePowerValue switch
        {
            > 300 => 100,
            > 250 => 50,
            > 200 => 25,
            > 150 => 20,
            > 100 => 15,
            > 75 => 10,
            > 50 => 5,
            > 25 => 4,
            > 20 => 3,
            > 15 => 2,
            > 10 => 1,
            _ => 0
        };

        ApiServiceInfo.LastPowerValue = ApiServiceInfo.DifferencePowerValue >= 50 ? (int)(Math.Round(ApiServiceInfo.LastOutputValueSum / 2 / 5.0) * 5) : (int)ApiServiceInfo.LastOutputValueSum / 2;
        ApiServiceInfo.NewPowerValue = ApiServiceInfo.LastPowerValue;

        // Prüfe, ob der aktuelle Preis über dem Tagesdurchschnitt liegt
        if (ApiServiceInfo.AvgPowerLoad > ApiServiceInfo.SettingOffsetAvg + (ApiServiceInfo.SettingToleranceAvg / 2))
        {
            ApiServiceInfo.NewPowerValue = ApiServiceInfo.LastPowerValue + ApiServiceInfo.DeltaPowerValue;
        }
        else if (ApiServiceInfo.AvgPowerLoad < ApiServiceInfo.SettingOffsetAvg - (ApiServiceInfo.SettingToleranceAvg / 2))
        {
            ApiServiceInfo.NewPowerValue = ApiServiceInfo.LastPowerValue - ApiServiceInfo.DeltaPowerValue;
        }

        var powerChanged = ApiServiceInfo.NewPowerValue != ApiServiceInfo.LastPowerValue;
        var maxPower = ApiServiceInfo.SettingMaxPower / 2;

        ApiServiceInfo.NewPowerValue = ApiServiceInfo.NewPowerValue > maxPower ? maxPower : ApiServiceInfo.NewPowerValue < 0 ? 0 : ApiServiceInfo.NewPowerValue;

        if (ApiServiceInfo.NewPowerValue <= maxPower && powerChanged)
        {
            foreach (var device in ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah"))
            {
                var last = ApiServiceInfo.GetLastValuePerDevice(device.DeviceSn);
                if (last == 0 || ApiServiceInfo.NewPowerValue == 0 || last != ApiServiceInfo.NewPowerValue)
                {
                    var item = new ApiOutputValueDeviceInfo()
                    {
                        DeviceType = device.DeviceType,
                        DeviceSn = device.DeviceSn,
                        Value = ApiServiceInfo.NewPowerValue,
                        TS = value.Timestamp.DateTime
                    };

                    lock (_growattSetPowerQueueLock)
                    {
                        ApiServiceInfo.GrowattSetPowerQueue.Enqueue(item);
                    }
                }
            }
        }
        else
        {
            Debug.WriteLine($"PowerChanged: {powerChanged}, OffSet: {ApiServiceInfo.SettingOffsetAvg}");
        }

        ApiServiceInfo.InvokeStateHasChanged();
        await CheckGrowattValueChangeQueue();
    }

    private async Task EnableAutoMode()
    {
        await SetNoTimerDeviceNoahs(this, new EventArgs());
    }

    private async Task DisableAutoMode(DateTime dateTime)
    {
        if (ApiServiceInfo.SettingLoadBalanced)
            await SetNoTimerDeviceNoahs(this, new EventArgs());

        foreach (var device in ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah"))
        {
            try
            {
                var item = new ApiOutputValueDeviceInfo()
                {
                    Force = true,
                    DeviceType = device.DeviceType,
                    DeviceSn = device.DeviceSn,
                    Value = 0,
                    TS = dateTime
                };
                ApiServiceInfo.GrowattSetPowerQueue.Enqueue(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            await Task.Delay(1000);
        }
    }

    public async Task GetDataFromTibber()
    {
        try
        {
            var basicData = await TibberApiClient.GetBasicData();
            TibberHomeId = basicData.Data.Viewer.Homes.FirstOrDefault()?.Id;

            if (TibberHomeId.HasValue)
            {
                var consumption = await TibberApiClient.GetHomeConsumption(TibberHomeId.Value, EnergyResolution.Monthly);

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
                var result = await TibberApiClient.Query(customQuery);

                var query = new TibberQueryBuilder().WithHomeConsumption(TibberHomeId.Value, EnergyResolution.Monthly, 12).Build();
                var response = await TibberApiClient.Query(query);
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
                var result = await TibberApiClient.Query(customQuery);

                await SavePrices(result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Tomorrow);
                await SavePrices(result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Today);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task SavePrices(ICollection<Price> prices)
    {
        var dbContext = GetDbContext();

        // Verarbeitung der Ergebnisse

        foreach (var item in prices)
        {
            var price = new ApiPrice()
            {
                StartsAt = DateTime.Parse(item.StartsAt),
                Total = item.Total,
                Level = item.Level
            };

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

    private GrowattApiClient GrowattApiClient { get; set; }

    private readonly Lock _growattSetPowerQueueLock = new Lock();

    private async Task CheckGrowattValueChangeQueue()
    {
        if (isGrowattValueChangeQueueRunning)
            return;

        isGrowattValueChangeQueueRunning = true;

        while (ApiServiceInfo.GrowattSetPowerQueue.Count > 0)
        {
            if (ApiServiceInfo.GrowattSetPowerQueue.Count > 5) ApiServiceInfo.GrowattSetPowerQueue.Clear();

            ApiOutputValueDeviceInfo? apiOutputValueDeviceInfo;

            lock (_growattSetPowerQueueLock)
            {
                ApiServiceInfo.GrowattSetPowerQueue.TryDequeue(out apiOutputValueDeviceInfo);
            }

            if (apiOutputValueDeviceInfo != null)
            {
                try
                {
                    await SetPower(apiOutputValueDeviceInfo);

                    if (penaltyFrequentlyAccess >= 10)
                        penaltyFrequentlyAccess -= 10;

                    await SetRealTimeMeasurement(apiOutputValueDeviceInfo);

                    ApiServiceInfo.InvokeStateHasChanged();

                    Trace.WriteLine($"CheckGrowattValueChangeQueue: {apiOutputValueDeviceInfo.DeviceSn}, {apiOutputValueDeviceInfo.Value}");
                }
                catch (Exception ex)
                {
                    lock (_growattSetPowerQueueLock)
                    {
                        if (apiOutputValueDeviceInfo.Force)
                            ApiServiceInfo.GrowattSetPowerQueue.Enqueue(apiOutputValueDeviceInfo);

                        ApiServiceInfo.GrowattSetPowerQueue = new Queue<ApiOutputValueDeviceInfo>(ApiServiceInfo.GrowattSetPowerQueue.Where(item => item.Force));
                    }

                    penaltyFrequentlyAccess += 50;

                    Logger.LogError(ex, $"Growatt Api Error: {ex.Message}");
                    Trace.WriteLine($"SettingLockSeconds: {ApiServiceInfo.SettingLockSeconds}, Penalty: {penaltyFrequentlyAccess}");
                }
            }
            await Task.Delay(ApiServiceInfo.SettingLockSeconds + penaltyFrequentlyAccess);
        }

        isGrowattValueChangeQueueRunning = false;
    }

    private async Task SetRealTimeMeasurement(ApiOutputValueDeviceInfo valueChange)
    {
        var item = ApiServiceInfo.RealTimeMeasurement.FirstOrDefault(x => x.TS == valueChange.TS);
        if (item != null)
        {
            if (item.DeviceInfos == null)
                item.DeviceInfos = [];

            var info = item.DeviceInfos.FirstOrDefault(x => x.DeviceSn == valueChange.DeviceSn);
            if (info != null)
            {
                info.TS = valueChange.TS;
                info.Value = valueChange.Value;
            }
            else
                item.DeviceInfos.Add(valueChange);
        }

        var dbContext = ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var data = dbContext.RealTimeMeasurements.FirstOrDefault(x => x.TS == valueChange.TS);
        if (data != null)
        {
            if (data.DeviceInfos == null)
                data.DeviceInfos = [];

            var info = data.DeviceInfos.FirstOrDefault(x => x.DeviceSn == valueChange.DeviceSn);
            if (info != null)
            {
                info.TS = valueChange.TS;
                info.Value = valueChange.Value;
            }
            else
                data.DeviceInfos.Add(valueChange);

            await dbContext.SaveChangesAsync();
        }
    }

    private async Task SetPower(ApiOutputValueDeviceInfo valueChange)
    {

        if (ApiServiceInfo.SettingLoadBalanced)
        {
            await GrowattApiClient.SetTimeSegmentAsync(new DeviceTimeSegment()
            {
                DeviceType = valueChange.DeviceType,
                DeviceSn = valueChange.DeviceSn,
                Type = "0",
                Enable = "1",
                StartTime = "00:00",
                EndTime = "23:59",
                Mode = "0",
                Power = valueChange.Value.ToString()
            });
        }
        else
            await GrowattApiClient.SetPowerAsync(valueChange.DeviceSn, "noah", valueChange.Value);

    }

    private async Task LoadDeviceData()
    {
        if (ApiServiceInfo.DataReads.Any(x => x.MethodeName == "GetDeviceLastDataAsync" && x.TimeStamp > DateTime.Now.AddMinutes(-1)))
        {
            var entry = new ApiCallLog()
            {
                MethodeName = "GetDeviceLastDataAsync",
                TimeStamp = DateTime.Now,
                RaisedError = false
            };

            try
            {
                var deviceSnList = string.Join(",", ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());

                List<DeviceNoahLastData>? deviceNoahLastDatas = null;

                deviceNoahLastDatas = await GrowattApiClient.GetDeviceLastDataAsync("noah", deviceSnList);

                if (deviceNoahLastDatas != null)
                {
                    await SaveDeviceNoahLastData(deviceNoahLastDatas);
                }
            }
            catch (Exception)
            {
                entry.RaisedError = true;
            }

            ApiServiceInfo.DataReads.Add(entry);
        }
    }

    private async Task RefreshDeviceList(object sender, EventArgs e)
    {
        await GetDevice();
        await RefreshDeviceNoahs(sender, e);
    }

    private async Task RefreshDeviceNoahs(object sender, EventArgs e)
    {
        var deviceSnList = string.Join(",", ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());
        await GetDeviceNoahInfo("noah", deviceSnList);
        await GetDeviceNoahLastDataAsync("noah", deviceSnList);
    }

    private async Task RefreshDeviceNoahLastData(object sender, EventArgs e)
    {
        var deviceSnList = string.Join(",", ApiServiceInfo.Devices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());
        await GetDeviceNoahLastDataAsync("noah", deviceSnList);
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

    private async Task GetDeviceNoahLastDataAsync(string deviceType, string deviceSn)
    {
        int penaltyFrequentlyAccess = 0;
        List<DeviceNoahLastData>? deviceNoahLastDatas = null;

        while (deviceNoahLastDatas == null)
        {
            try
            {
                deviceNoahLastDatas = await GrowattApiClient.GetDeviceLastDataAsync(deviceType, deviceSn);
                penaltyFrequentlyAccess = 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
                penaltyFrequentlyAccess += 100;
                await Task.Delay(1000 + penaltyFrequentlyAccess);
            }
        }

        if (deviceNoahLastDatas != null)
        {
            await SaveDeviceNoahLastData(deviceNoahLastDatas);
        }
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

    #region Configs

    private async Task SetNoTimerDeviceNoahs(object sender, EventArgs e)
    {
        Queue<DeviceTimeSegment> DeviceTimeSegmentQueue = new();

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
                    var request = new DeviceTimeSegment
                    {
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

        while (DeviceTimeSegmentQueue.Count > 0)
        {
            var segment = DeviceTimeSegmentQueue.Dequeue();
            try
            {
                await GrowattApiClient.SetTimeSegmentAsync(segment);
            }
            catch (Exception ex)
            {
                DeviceTimeSegmentQueue.Enqueue(segment);
                Console.WriteLine(ex.Message);
            }

            await Task.Delay(ApiServiceInfo.SettingLockSeconds);
        }
    }

    #endregion

    #endregion Growatt

    #endregion Methodes
}
