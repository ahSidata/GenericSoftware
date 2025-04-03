using EnergyAutomate.Definitions;
using Growatt.OSS;
using Growatt.Sdk;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Tibber.Sdk;

namespace EnergyAutomate.Services
{
    public partial class ApiService : IObserver<RealTimeMeasurement>
    {
        #region Fields

        private readonly Lock lockAdjustPower = new();
        private readonly Lock lockLoadBalance = new();
        private readonly int timerPenalty = 0;

        #endregion Fields

        #region Public Constructors

        public ApiService(IServiceProvider serviceProvider)
        {
            CurrentState = new ApiState(this);
            ServiceProvider = serviceProvider;
            GrowattDeviceQueryQueueWatchdog.OnItemDequeued += GrowattDeviceQueryQueueWatchdog_OnItemDequeued;
            TibberRealTimeMeasurement.CollectionChanged += (sender, e) => { GrowattRealTimeMeasurementChanged?.Invoke(sender, e); };
        }

        #endregion Public Constructors

        #region Events

        public event EventHandler? GrowattRealTimeMeasurementChanged;

        public event EventHandler? StateHasChanged;

        #endregion Events

        #region Properties

        public bool ApiSettingAutoMode { get; set; }
        public bool ApiSettingAutoModeRestrictionCurrentState { get; set; } = false;
        public List<APiTraceValue> ApiSettingAvgPowerAdjustmentTraceValues { get; set; } = [];
        public int ApiSettingAvgPowerHysteresis { get; set; } = 50;
        public int ApiSettingAvgPowerLoadSeconds { get; set; } = 0;
        public int ApiSettingAvgPowerOffset { get; set; } = 25;
        public bool ApiSettingBatteryPriorityMode { get; set; } = false;
        public Dictionary<string, int> ApiSettingDataReadsDelaySec { get; set; } = new Dictionary<string, int>() {
            { nameof(DeviceNoahInfoQuery), 60 * 2} ,
            { nameof(DeviceNoahLastDataQuery), 60 } ,
            { nameof(DeviceNoahSetPowerQuery), 60 } ,
            { nameof(DeviceNoahTimeSegmentQuery), 60 } ,
            { nameof(DeviceListQuery), 60 * 5 }
        };
        public int ApiSettingMaxPower { get; set; } = 840;
        public bool ApiSettingRestrictionMode { get; set; } = false;
        public int ApiSettingTimeOffset { get; set; } = DateTimeOffset.Now.Offset.Hours;
        public ApiState CurrentState { get; set; }
        public TimeOnly SunRise { get; set; } = new TimeOnly(8, 0);
        public TimeOnly SunSet { get; set; } = new TimeOnly(16, 0);
        private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog => ServiceProvider.GetRequiredService<ApiRealTimeMeasurementWatchdog>();
        private GrowattApiClient GrowattApiClient => ServiceProvider.GetRequiredService<GrowattApiClient>();
        private List<ApiCallLog> GrowattDataReads { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceNoahInfo> GrowattDeviceNoahInfo { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceNoahLastData> GrowattDeviceNoahLastData { get; set; } = [];
        private ApiQueueWatchdog<IDeviceQuery> GrowattDeviceQueryQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<IDeviceQuery>>();
        private ThreadSafeObservableCollection<DeviceList> GrowattDevices { get; set; } = [];
        private ILogger Logger => ServiceProvider.GetRequiredService<ILogger<ApiService>>();
        private IServiceProvider ServiceProvider { get; set; }
        private Guid? TibberHomeId { get; set; }
        private ThreadSafeObservableCollection<TibberPrice> TibberPrices { get; set; } = [];
        private ThreadSafeObservableCollection<RealTimeMeasurementExtention> TibberRealTimeMeasurement { get; set; } = [];

        #endregion Properties

        #region Public Methods

        public bool ApiGetCurrentAutoModeRestriction()
        {
            var level = TibberPrices.OrderBy(o => o.StartsAt).FirstOrDefault(x => x.StartsAt > CurrentState.Now.AddHours(-1))?.Level ?? PriceLevel.Normal;
            return level == PriceLevel.Expensive || level == PriceLevel.VeryExpensive;
        }

        public void ApiInvokeStateHasChanged()
        {
            StateHasChanged?.Invoke(this, new EventArgs());
        }

        public async Task ApiLoadDataFromDatabase()
        {
            var dbContext = ApiGetDbContext();

            var devices = await dbContext.Devices.ToListAsync();
            GrowattDevices.Clear();
            foreach (var device in devices)
            {
                GrowattDevices.Add(device);
            }

            var deviceNoahInfoList = await dbContext.DeviceNoahInfo.ToListAsync();
            GrowattDeviceNoahInfo.Clear();
            foreach (var info in deviceNoahInfoList)
            {
                GrowattDeviceNoahInfo.Add(info);
            }

            var deviceNoahLastDataList = await dbContext.DeviceNoahLastData.ToListAsync();
            GrowattDeviceNoahLastData.Clear();
            foreach (var lastData in deviceNoahLastDataList)
            {
                GrowattDeviceNoahLastData.Add(lastData);
            }

            var realTimeMeasurements = await dbContext.RealTimeMeasurements.OrderByDescending(x => x.Timestamp).Take(100).ToListAsync();
            TibberRealTimeMeasurement.Clear();
            foreach (var measurement in realTimeMeasurements)
            {
                TibberRealTimeMeasurement.Add(measurement);
            }
        }

        public async Task ApiStartAsync(CancellationToken cancellationToken)
        {
            var tibberClient = ServiceProvider.GetRequiredService<TibberApiClient>();

            var basicData = await tibberClient.GetBasicData(cancellationToken);
            TibberHomeId = basicData.Data.Viewer.Homes.FirstOrDefault()?.Id;

            await TibberGetTomorrowPrices();
            await ApiLoadDataFromDatabase();
        }

        public async Task ApiStopAsync(CancellationToken cancellationToken)
        {
        }

        public bool GrowattDataReadsDoRefresh(string queryType, int? delay = null)
        {
            return !GrowattDataReads.Where(x => x.MethodeName == queryType && x.TimeStamp > CurrentState.Now.AddSeconds(-(delay ?? ApiSettingDataReadsDelaySec[queryType]))).Any();
        }

        public int GrowattDeviceQueryQueueWatchdogCount()
        {
            return GrowattDeviceQueryQueueWatchdog.Count;
        }

        public Task GrowattGetDevice(bool force = false)
        {
            GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceListQuery() { Force = force });

            return Task.CompletedTask;
        }

        public Task GrowattGetDeviceNoahInfo(bool force = false)
        {
            if (CurrentState.IsGrowattOnline)
            {
                var deviceSnList = GetGrowattDeviceSnList();

                GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahInfoQuery()
                {
                    Force = false,
                    DeviceType = "noah",
                    DeviceSn = deviceSnList,
                });
            }

            return Task.CompletedTask;
        }

        public Task GrowattGetDeviceNoahLastData(bool force = false)
        {
            if (CurrentState.IsGrowattOnline)
            {
                var deviceSnList = GetGrowattDeviceSnList();

                GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahLastDataQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = deviceSnList,
                    Force = force
                });
            }

            return Task.CompletedTask;
        }

        public int? GrowattGetLastCommitedPowerValue() => GrowattGetLastCommitedPowerValueItem()?.CommitedPowerValue;

        public RealTimeMeasurementExtention? GrowattGetLastCommitedPowerValueItem()
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                return TibberRealTimeMeasurement.Where(x => x.Timestamp > CurrentState.Now.AddDays(-1) && x.CommitedPowerValue != null).OrderByDescending(x => x.Timestamp).FirstOrDefault();
            }
        }

        public int? GrowattGetLastRequestedPowerValue() => GrowattGetLastRequestedPowerValueItem()?.RequestedPowerValue;

        public RealTimeMeasurementExtention? GrowattGetLastRequestedPowerValueItem()
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                return TibberRealTimeMeasurement.Where(x => x.Timestamp > CurrentState.Now.AddDays(-1) && x.RequestedPowerValue != null).OrderByDescending(x => x.Timestamp).FirstOrDefault();
            }
        }

        public int GrowattGetNoahCurrentIsDischarchingState()
        {
            return GrowattDevices
                .Where(w => w.DeviceType == "noah")
                .Max(noah => GrowattGetNoahLastDataPerDevice(noah.DeviceSn)?.totalBatteryPackChargingStatus ?? 0);
        }

        public int GrowattGetNoahDeviceCount()
        {
            return GrowattDevices.Where(x => x.DeviceType == "noah").Count();
        }

        public DeviceNoahInfo? GrowattGetNoahInfoPerDevice(string deviceSn)
        {
            lock (GrowattDeviceNoahInfo._syncRoot)
            {
                return GrowattDeviceNoahInfo.FirstOrDefault(x => x.DeviceSn == deviceSn);
            }
        }

        public DeviceNoahLastData? GrowattGetNoahLastDataPerDevice(string deviceSn)
        {
            lock (GrowattDeviceNoahLastData._syncRoot)
            {
                var result = GrowattDeviceNoahLastData.Where(x => x.deviceSn == deviceSn).OrderByDescending(x => x.time).FirstOrDefault();
                return result;
            }
        }

        public async Task GrowattInvokeBattPriorityDeviceNoah()
        {
            await GrowattSetBattPriorityDeviceNoahTimeSegmentsAsync();
        }

        public async Task GrowattInvokeClearDeviceNoahTimeSegments()
        {
            await GrowattGetDeviceNoahInfo(true);
            await Task.Delay(5000);
            await GrowattSetNoDeviceNoahTimeSegments();
        }

        public async Task GrowattInvokeLoadPriorityDeviceNoah()
        {
            await GrowattSetLoadPriorityDeviceNoahTimeSegmentsAsync();
        }

        public Task GrowattInvokeRefreshDeviceList()
        {
            GrowattGetDevice(true);
            GrowattGetDeviceNoahInfo(true);
            GrowattGetDeviceNoahLastData(true);

            return Task.CompletedTask;
        }

        public Task GrowattInvokeRefreshNoahs()
        {
            GrowattGetDeviceNoahInfo(true);
            GrowattGetDeviceNoahLastData(true);

            return Task.CompletedTask;
        }

        public Task GrowattInvokeRefreshNoahsLastData()
        {
            GrowattGetDeviceNoahLastData(true);

            return Task.CompletedTask;
        }

        public List<DeviceList> GrowattListDevices()
        {
            lock (GrowattDevices._syncRoot)
                return GrowattDevices.ToList();
        }

        public List<DeviceNoahInfo> GrowattListNoahInfos()
        {
            lock (GrowattDeviceNoahInfo._syncRoot)
                return GrowattDeviceNoahInfo.ToList();
        }

        public List<DeviceNoahLastData> GrowattListNoahLastDatas()
        {
            lock (GrowattDeviceNoahLastData._syncRoot)
                return GrowattDeviceNoahLastData.ToList();
        }

        public async Task GrowattSetProgramActive(GrowattProgram growattProgram)
        {
            await Task.CompletedTask;
        }

        public async Task TibberGetDataFromWeb()
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

        public List<TibberPrice> TibberGetPriceDatas()
        {
            var items = TibberPrices.OrderByDescending(x => x.StartsAt).Take(48).ToList();
            return items.OrderBy(x => x.StartsAt).ToList();
        }

        public async Task TibberGetTomorrowPrices()
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

                    await TibberSavePrices(result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Tomorrow.Select(x => new TibberPrice()
                    {
                        StartsAt = DateTimeOffset.Parse(x.StartsAt),
                        Total = x.Total,
                        Level = x.Level
                    }).ToList()
                    );
                    await TibberSavePrices(result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Today.Select(x => new TibberPrice()
                    {
                        StartsAt = DateTimeOffset.Parse(x.StartsAt),
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

        public List<TibberPrice> TibberListPrices()
        {
            lock (TibberPrices._syncRoot)
                return TibberPrices.ToList();
        }

        public List<RealTimeMeasurementExtention> TibberListRealTimeMeasurement()
        {
            lock (TibberRealTimeMeasurement._syncRoot)
                return TibberRealTimeMeasurement.ToList();
        }

        #endregion Public Methods

        #region Private Methods

        private async Task ApiAutoModeDisabledLoadBalanceRule()
        {
            // If the automatic mode is disabled, the power value is set to 0
            await GrowattClearPowerSetAsync();

            if (ApiSettingBatteryPriorityMode)
            {
                Trace.WriteLine($"LoadBalanced: Set BattPriority", "ApiService");
                //If loadbalance is active the battety priority is set
                await GrowattSetBattPriorityDeviceNoahTimeSegmentsAsync();
            }
            else
            {
                Trace.WriteLine($"LoadBalanced: Set LoadPriority", "ApiService");

                //If loadbalance is not active the load priority is set
                // Calc avg power value
                await GrowattSetLoadPriorityDeviceNoahTimeSegmentsAsync();
            }
        }

        private async Task ApiAutoModeEnabledLoadBalanceRule()
        {
            // If the automatic mode is enabled and the restriction is not active, the power value
            // is set to 0
            await GrowattClearPowerSetAsync();
        }

        private ApplicationDbContext ApiGetDbContext()
        {
            return ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
        }

        private Task ApiLoadBalance(RealTimeMeasurementExtention value)
        {
            if (ApiSettingAutoMode && CurrentState.IsGrowattOnline)
            {
            }
            else
            {
                if (ApiSettingBatteryPriorityMode)
                {
                }
                Trace.WriteLine($"AutoMode disabled: Running LoadBalanceRule one time", "ApiService");
            }
            return Task.CompletedTask;
        }

        private string GetGrowattDeviceSnList()
        {
            return string.Join(",", GrowattDevices.Where(x => x.IsOfflineSince == null).Select(x => x.DeviceSn).ToList());
        }

        private Task GrowattClearDeviceNoahTimeSegments(Queue<IDeviceQuery> DeviceTimeSegmentQueue)
        {
            var deviceSnList = GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

            foreach (var deviceSn in deviceSnList)
            {
                var data = GrowattDeviceNoahInfo.FirstOrDefault(x => x.DeviceSn == deviceSn);
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

        private Task GrowattClearPowerSetAsync(int value = 0)
        {
            var devices = GrowattDevices.Where(x => x.DeviceType == "noah").ToList();

            foreach (var device in devices)
            {
                GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahSetPowerQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = device.DeviceSn,
                    Value = value,
                    Force = true
                });
            }

            return Task.CompletedTask;
        }

        private async Task<ApiException?> GrowattDeviceQueryQueueWatchdog_OnItemDequeued(IDeviceQuery item, GrowattApiClient growattApiClient)
        {
            var dbContext = ApiGetDbContext();
            RealTimeMeasurementExtention? dataRealTimeMeasurementApiService = null;
            RealTimeMeasurementExtention? dataRealTimeMeasurementDbContext = null;
            DeviceList? device = null;

            var entry = new ApiCallLog()
            {
                MethodeName = item.GetType().Name,
                TimeStamp = CurrentState.Now,
                RaisedError = false
            };

            switch (item)
            {
                case DeviceNoahSetPowerQuery setPowerQuery:
                    if (setPowerQuery.Force || (setPowerQuery != null && !string.IsNullOrWhiteSpace(setPowerQuery.DeviceType) && !string.IsNullOrWhiteSpace(setPowerQuery.DeviceSn)))
                    {
                        device = GrowattDevices.FirstOrDefault(x => x.DeviceSn == setPowerQuery.DeviceSn);
                        if (setPowerQuery.TS.HasValue)
                        {
                            dataRealTimeMeasurementApiService = TibberRealTimeMeasurement.FirstOrDefault(x => x.TS == setPowerQuery.TS);
                            dataRealTimeMeasurementDbContext = dbContext.RealTimeMeasurements.FirstOrDefault(x => x.TS == setPowerQuery.TS);
                        }

                        try
                        {
                            //Add log entry
                            GrowattDataReads.Add(entry);

                            await growattApiClient.SetPowerAsync(item);

                            if (device != null)
                                lock (GrowattDevices._syncRoot)
                                    device.PowerValueCommited = setPowerQuery.Value;

                            if (dataRealTimeMeasurementApiService != null)
                                lock (TibberRealTimeMeasurement._syncRoot)
                                    dataRealTimeMeasurementApiService.CommitedPowerValue = setPowerQuery.Value;

                            if (dataRealTimeMeasurementDbContext != null)
                            {
                                dataRealTimeMeasurementDbContext.CommitedPowerValue = setPowerQuery.Value;
                                await dbContext.SaveChangesAsync();
                            }

                            Trace.WriteLine($"Commited lastRequestedPowerValue: {setPowerQuery.Value} W", "ApiService");

                            ApiInvokeStateHasChanged();

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
                            GrowattDataReads.Add(entry);

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

                    if (lastDataQuery.Force || (lastDataQuery != null && !string.IsNullOrWhiteSpace(lastDataQuery.DeviceType) && !string.IsNullOrWhiteSpace(lastDataQuery.DeviceSn)))
                    {
                        try
                        {
                            if (GrowattDataReadsDoRefresh(item.GetType().Name))
                            {
                                //Add log entry
                                GrowattDataReads.Add(entry);

                                //Refresh Last data ever minute
                                var deviceNoahLastDatas = await growattApiClient.GetDeviceLastDataAsync(lastDataQuery);
                                if (deviceNoahLastDatas != null)
                                {
                                    await GrowattSaveDeviceNoahLastData(deviceNoahLastDatas);
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

                    if (infoQuery.Force || (item != null && !string.IsNullOrWhiteSpace(item.DeviceType) && !string.IsNullOrWhiteSpace(item.DeviceSn)))
                    {
                        try
                        {
                            if (GrowattDataReadsDoRefresh(item.GetType().Name))
                            {
                                //Add log entry
                                GrowattDataReads.Add(entry);

                                //Refresh Last data ever minute
                                var deviceNoahInfos = await growattApiClient.GetDeviceInfoAsync(infoQuery);
                                if (deviceNoahInfos != null)
                                {
                                    await GrowattSaveDeviceNoahInfo(deviceNoahInfos);
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
                case DeviceListQuery infoQuery:

                    List<DeviceList>? deviceLists = null;

                    try
                    {
                        if (GrowattDataReadsDoRefresh(item.GetType().Name))
                        {
                            //Add log entry
                            GrowattDataReads.Add(entry);
                            deviceLists = await GrowattApiClient.GetDeviceListAsync();

                            if (deviceLists != null)
                            {
                                await GrowattSaveDeviceList(deviceLists);
                            }

                            return default; // Operation erfolgreich
                        }
                    }
                    catch (ApiException ex)
                    {
                        return ex; // Operation fehlgeschlagen
                    }

                    return default;
                default:
                    return default;
            }
        }

        private async Task GrowattSaveDeviceList(List<DeviceList> deviceLists)
        {
            var dbContext = ApiGetDbContext();

            foreach (var deviceList in deviceLists)
            {
                lock (GrowattDevices._syncRoot)
                {
                    var apiServiceDeviceNoah = GrowattDevices.FirstOrDefault(x => x.DeviceSn == deviceList.DeviceSn);
                    if (apiServiceDeviceNoah != null) GrowattDevices.Remove(apiServiceDeviceNoah);
                    GrowattDevices.Add(deviceList);
                }

                var existingDevice = await dbContext.Devices.FindAsync(deviceList.DeviceSn);
                if (existingDevice != null)
                {
                    dbContext.Entry(existingDevice).CurrentValues.SetValues(deviceList);
                }
                else
                {
                    dbContext.Devices.Add(deviceList);
                }
            }

            await dbContext.SaveChangesAsync();

            ApiInvokeStateHasChanged();
        }

        private async Task GrowattSaveDeviceNoahInfo(List<DeviceNoahInfo> deviceNoahInfos)
        {
            var dbContext = ApiGetDbContext();

            foreach (var deviceNoahInfo in deviceNoahInfos)
            {
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(deviceNoahInfo.LastUpdateTime).DateTime;
                var offset = TimeSpan.FromHours(-6); // Beispiel: Offset von 2 Stunden
                deviceNoahInfo.TS = new DateTimeOffset(dateTime, offset);

                lock (GrowattDevices._syncRoot)
                {
                    var deviceApiService = GrowattDevices.FirstOrDefault(x => x.DeviceType == "noah" && x.DeviceSn == deviceNoahInfo.DeviceSn);
                    if (deviceApiService != null)
                    {
                        deviceApiService.IsOfflineSince = deviceNoahInfo.Lost ? new DateTime(deviceNoahInfo.LastUpdateTime) : null;
                    }
                }

                lock (GrowattDeviceNoahInfo._syncRoot)
                {
                    var apiServiceDeviceNoahInfo = GrowattDeviceNoahInfo.FirstOrDefault(x => x.DeviceSn == deviceNoahInfo.DeviceSn);
                    if (apiServiceDeviceNoahInfo != null) GrowattDeviceNoahInfo.Remove(apiServiceDeviceNoahInfo);
                    GrowattDeviceNoahInfo.Add(deviceNoahInfo);
                }

                var deviceDbContext = dbContext.Devices.FirstOrDefault(x => x.DeviceType == "noah" && x.DeviceSn == deviceNoahInfo.DeviceSn);
                if (deviceDbContext != null)
                {
                    deviceDbContext.IsOfflineSince = deviceNoahInfo.Lost ? new DateTime(deviceNoahInfo.LastUpdateTime) : null;
                }

                var dbContextDeviceNoahInfo = await dbContext.DeviceNoahInfo.FindAsync(deviceNoahInfo.DeviceSn);
                if (dbContextDeviceNoahInfo != null)
                {
                    dbContext.Entry(dbContextDeviceNoahInfo).CurrentValues.SetValues(deviceNoahInfo);
                }
                else
                {
                    dbContext.DeviceNoahInfo.Add(deviceNoahInfo);
                }
            }

            // Save changes to the database
            await dbContext.SaveChangesAsync();

            ApiInvokeStateHasChanged();
        }

        private async Task GrowattSaveDeviceNoahLastData(List<DeviceNoahLastData> deviceNoahLastDatas)
        {
            var dbContext = ApiGetDbContext();

            foreach (var deviceNoahLastData in deviceNoahLastDatas)
            {
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(deviceNoahLastData.time).DateTime;
                var offset = TimeSpan.FromHours(-6); // Beispiel: Offset von 2 Stunden
                deviceNoahLastData.TS = new DateTimeOffset(dateTime, offset);

                lock (GrowattDevices._syncRoot)
                {
                    var device = GrowattDevices.FirstOrDefault(x => x.DeviceSn == deviceNoahLastData.deviceSn);
                    if (device != null)
                    {
                        device.PowerValueRequested = (int)deviceNoahLastData.pac;
                        device.PowerValueCommited = (int)deviceNoahLastData.pac;
                    }
                }

                lock (GrowattDeviceNoahLastData._syncRoot)
                {
                    GrowattDeviceNoahLastData.Add(deviceNoahLastData);
                }

                var deviceDbContext = await dbContext.Devices.FindAsync(deviceNoahLastData.deviceSn);
                if (deviceDbContext != null)
                {
                    deviceDbContext.PowerValueRequested = (int)deviceNoahLastData.pac;
                    deviceDbContext.PowerValueCommited = (int)deviceNoahLastData.pac;
                }

                var dbContextDeviceNoahLastData = await dbContext.DeviceNoahLastData.FindAsync(deviceNoahLastData.deviceSn, deviceNoahLastData.time);
                if (dbContextDeviceNoahLastData != null)
                {
                    dbContext.Entry(dbContextDeviceNoahLastData).CurrentValues.SetValues(deviceNoahLastData);
                }
                else
                {
                    dbContext.DeviceNoahLastData.Add(deviceNoahLastData);
                }
            }

            await dbContext.SaveChangesAsync();

            ApiInvokeStateHasChanged();
        }

        private async Task GrowattSetBattPriorityDeviceNoahTimeSegmentsAsync()
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

            var deviceSnList = GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

            foreach (var deviceSn in deviceSnList)
            {
                DeviceTimeSegmentQueue.Enqueue(new DeviceNoahTimeSegmentQuery()
                {
                    Force = true,
                    DeviceSn = deviceSn,
                    DeviceType = "noah",
                    Type = "1",
                    StartTime = "08:00",
                    EndTime = "15:59",
                    Mode = "1",
                    Power = "0",
                    Enable = "1"
                });
            }

            GrowattDeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
        }

        private async Task GrowattSetLoadPriorityDeviceNoahTimeSegmentsAsync(int powerValue = 0)
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

            var deviceSnList = GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

            var powerValuePerDevice = powerValue / deviceSnList.Count;

            foreach (var deviceSn in deviceSnList)
            {
                DeviceTimeSegmentQueue.Enqueue(new DeviceNoahTimeSegmentQuery()
                {
                    Force = true,
                    DeviceSn = deviceSn,
                    DeviceType = "noah",
                    Type = "1",
                    StartTime = "08:00",
                    EndTime = "23:59",
                    Mode = "0",
                    Power = powerValuePerDevice.ToString(),
                    Enable = "1"
                });
            }

            GrowattDeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
        }

        private Task GrowattSetNoDeviceNoahTimeSegments()
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            GrowattClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

            GrowattDeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);

            return Task.CompletedTask;
        }

        private Task TibberRTMAdjustPower(RealTimeMeasurementExtention value)
        {
            int calcPowerValue = 0;
            int newPowerValue = 0;

            var devices = GrowattDevices.Where(x => x.DeviceType == "noah" && x.IsOfflineSince == null).ToList();

            DeviceList? device = null;

            var last = devices.OrderBy(x => x.PowerValueLastChanged).FirstOrDefault();

            var upperlimit = ApiSettingAvgPowerOffset + (ApiSettingAvgPowerHysteresis / 2);
            var lowerlimit = ApiSettingAvgPowerOffset - (ApiSettingAvgPowerHysteresis / 2);

            int consumptionDelta = 0;
            int productionDelta = 0;

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 4, Key = "DeltaPowerValue", Value = productionDelta.ToString() });

            if (value.TotalPower > 0)
                device = devices.OrderBy(x => x.PowerValueCommited).FirstOrDefault();

            if (value.TotalPower < 0)
                device = devices.OrderByDescending(x => x.PowerValueCommited).FirstOrDefault();

            if (device != null)
            {
                int lastCommitedPowerValue = device.PowerValueCommited == 0 ? (int)(GrowattGetNoahLastDataPerDevice(device.DeviceSn)?.pac ?? 0) : device.PowerValueCommited;
                var lastRequestedPowerValue = device.PowerValueRequested;
                var avgPowerConsumption = value.AvgPowerConsumption;
                var avgPowerProduction = -value.AvgPowerProduction;

                // If the total power is greater than 0, it indicates power consumption
                if (value.TotalPower > 0)
                {
                    // If the average power consumption is greater than the upper limit
                    if (avgPowerConsumption > upperlimit)
                    {
                        // Calculate the difference between the average power consumption and the
                        // upper limit
                        consumptionDelta = Math.Abs(avgPowerConsumption - upperlimit);
                        // Add or update the trace value for the delta power value
                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 4, Key = "DeltaPowerValue", Value = consumptionDelta.ToString() });
                        // Calculate the new power value based on the consumption delta additional
                        // half for slower decreasing
                        calcPowerValue = lastCommitedPowerValue + (consumptionDelta / devices.Count);
                    }
                    // If the average power consumption is less than the lower limit
                    else if (avgPowerConsumption < lowerlimit)
                    {
                        // Calculate the difference between the lower limit and the average power consumption
                        consumptionDelta = Math.Abs(lowerlimit - avgPowerConsumption);
                        // Calculate the new power value based on the consumption delta
                        calcPowerValue = lastCommitedPowerValue - (consumptionDelta / devices.Count);
                    }
                }
                // If the total power is less than 0, it indicates power production
                else if (value.TotalPower < 0)
                {
                    // If the average power production is less than the lower limit
                    if (avgPowerProduction < lowerlimit)
                    {
                        // Calculate the difference between the average power production and the
                        // lower limit
                        productionDelta = Math.Abs(avgPowerProduction - lowerlimit);
                        // Add or update the trace value for the delta power value
                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 4, Key = "DeltaPowerValue", Value = productionDelta.ToString() });
                        // Calculate the new power value based on the production delta
                        calcPowerValue = lastCommitedPowerValue - (productionDelta / devices.Count);
                    }
                    // If the average power production is greater than the upper limit
                    else if (avgPowerProduction > upperlimit)
                    {
                        // Calculate the difference between the average power production and the
                        // upper limit
                        productionDelta = Math.Abs(avgPowerProduction - upperlimit);
                        // Calculate the new power value based on the production delta
                        calcPowerValue = lastCommitedPowerValue + (productionDelta / devices.Count);
                    }
                }

                var maxPower = ApiSettingMaxPower / devices.Count;

                newPowerValue = calcPowerValue > maxPower ? maxPower : calcPowerValue < 0 ? 0 : calcPowerValue;

                if (newPowerValue <= maxPower && newPowerValue > 0)
                {
                    if (newPowerValue != lastRequestedPowerValue)
                    {
                        device.PowerValueLastChanged = value.TS;
                        device.PowerValueRequested = newPowerValue;
                        value.RequestedPowerValue = newPowerValue;

                        GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahSetPowerQuery()
                        {
                            DeviceType = "noah",
                            DeviceSn = device.DeviceSn,
                            Value = newPowerValue,
                            TS = value.TS
                        });

                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 5, Key = "NewPowerValue", Value = newPowerValue.ToString() });
                    }
                }
                else
                {
                    if (value.TotalPower > 0)
                    {
                        Trace.WriteLine($"TotalPower: {value.TotalPower}, AvgPowerProduction: {avgPowerConsumption}, upperDelta: {consumptionDelta} = {avgPowerConsumption} - {upperlimit}", "ApiService");
                        Trace.WriteLine($"lastCommitedPowerValue: {lastCommitedPowerValue}, upperDelta: {consumptionDelta} = {avgPowerConsumption} - {upperlimit}, calcPowerValue: {calcPowerValue}, OffSet: {ApiSettingAvgPowerOffset}", "ApiService");
                    }
                    if (value.TotalPower < 0)
                    {
                        Trace.WriteLine($"TotalPower: {value.TotalPower}, AvgPowerProduction: {avgPowerProduction}, lowerDelta: {productionDelta} = {avgPowerProduction} - {lowerlimit}", "ApiService");
                        Trace.WriteLine($"lastCommitedPowerValue: {lastCommitedPowerValue} - lowerDelta: {productionDelta}, calcPowerValue: {calcPowerValue}, OffSet: {ApiSettingAvgPowerOffset}", "ApiService");
                    }
                }

                ApiInvokeStateHasChanged();
            }

            return Task.CompletedTask;
        }

        private Task TibberRTMCalcAvgOfLastSeconds(RealTimeMeasurementExtention value)
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                var measurementsQuery = TibberRealTimeMeasurement;

                if (ApiSettingAvgPowerLoadSeconds > 0)
                    measurementsQuery.Where(m => m.Timestamp >= CurrentState.Now.AddSeconds(-ApiSettingAvgPowerLoadSeconds));

                var measurements = measurementsQuery.ToList();

                measurements.Add(value);

                var powerConsumptionCompleteMeasurements = measurements.OrderByDescending(m => m.Timestamp).ToList().GetEnumerator();

                List<RealTimeMeasurementExtention> powerConsumptionUntilZero = [];

                while (powerConsumptionCompleteMeasurements.MoveNext())
                {
                    if (powerConsumptionCompleteMeasurements.Current.Power == 0) break;
                    powerConsumptionUntilZero.Add(powerConsumptionCompleteMeasurements.Current);
                }

                value.AvgPowerConsumption = value.Power > 0 ? (int)powerConsumptionUntilZero.Average(m => m.Power) : 0;

                var powerProductionCompleteMeasurements = measurements.OrderByDescending(m => m.Timestamp).ToList().GetEnumerator();

                List<RealTimeMeasurementExtention> powerProductionUntilZero = [];

                while (powerProductionCompleteMeasurements.MoveNext())
                {
                    if (powerProductionCompleteMeasurements.Current.PowerProduction == 0) break;
                    powerProductionUntilZero.Add(powerProductionCompleteMeasurements.Current);
                }

                value.AvgPowerProduction = value.PowerProduction > 0 ? (int)powerProductionUntilZero.Average(m => m.PowerProduction ?? 0) : 0;
            }

            return Task.CompletedTask;
        }

        private async Task TibberRTMProceeding(RealTimeMeasurementExtention value)
        {
            if (ApiSettingAutoMode && CurrentState.IsGrowattOnline)
            {
                // If the automatic mode is enabled, the power value is adjusted
                if (!CurrentState.IsRTMAutoModeRunning)
                {
                    //Clear all time segments
                    await GrowattSetNoDeviceNoahTimeSegments();

                    Trace.WriteLine($"Not in grace periode: Running LoadBalanceRule one time", "ApiService");
                    //If loadbalance is active the battety priority is set
                    await ApiAutoModeEnabledLoadBalanceRule();
                }

                // If the automatic mode is enabled
                if (ApiSettingRestrictionMode)
                {
                    var autoModeRestrictionCurrentState = ApiGetCurrentAutoModeRestriction();
                    ApiSettingAutoModeRestrictionCurrentState = autoModeRestrictionCurrentState;
                    value.SettingRestrictionState = autoModeRestrictionCurrentState;
                    value.SettingAutoMode = ApiSettingAutoMode;
                    value.SettingRestrictionState = ApiSettingRestrictionMode;
                    value.SettingBatteryPriorityMode = ApiSettingBatteryPriorityMode;

                    // If the automatic mode is enabled and the restriction is active, the power
                    // value is adjusted
                    if (autoModeRestrictionCurrentState)
                    {
                        if (!CurrentState.IsRTMRestrictionModeRunning)
                        {
                            Trace.WriteLine($"Are in grace periode: Running SetNoDeviceNoahTimeSegments one time", "ApiService");
                            //Clear all time segments
                            await GrowattSetNoDeviceNoahTimeSegments();
                        }

                        if (ApiSettingBatteryPriorityMode)
                        {
                            Trace.WriteLine($"Not in grace periode: Running LoadBalanceRule one time", "ApiService");
                            await TibberRTMAdjustPower(value);
                        }
                        else
                        {
                            lock (lockLoadBalance)
                            {
                                ApiLoadBalance(value);
                            }
                        }

                        CurrentState.IsRTMRestrictionModeRunning = true;
                    }
                    // If the automatic mode is enabled and the restriction is not active, the power
                    // value is set to 0
                    else
                    {
                        if (CurrentState.IsRTMRestrictionModeRunning)
                        {
                            //Clear all querys
                            GrowattDeviceQueryQueueWatchdog.Clear();

                            Trace.WriteLine($"AutoMode enabled, not in grace periode: Running LoadBalanceRule one time", "ApiService");

                            //If loadbalance is active the battety priority is set
                            //await ApiAutoModeDisabledLoadBalanceRule();

                            CurrentState.IsRTMRestrictionModeRunning = false;
                        }

                        Trace.WriteLine($"Not in grace periode: Nothing to do", "ApiService");
                        CurrentState.IsRTMRestrictionModeRunning = false;
                    }
                }
                else
                {
                    // If the automatic mode is enabled and the restriction is not active, the power
                    // value is set to 0

                    if (ApiSettingBatteryPriorityMode)
                    {
                        Trace.WriteLine($"AutoMode enabled: Running LoadBalanceRule one time", "ApiService");
                        lock (lockLoadBalance)
                        {
                            ApiLoadBalance(value);
                        }
                    }
                    else
                    {
                        await TibberRTMAdjustPower(value);
                    }
                }
                CurrentState.IsRTMAutoModeRunning = true;
            }
            else
            {
                if (CurrentState.IsGrowattOnline)
                {
                    // If the automatic mode is disabled, the power value is set to 0
                    if (CurrentState.IsRTMAutoModeRunning)
                    {
                        GrowattDeviceQueryQueueWatchdog.Clear();

                        Trace.WriteLine($"AutoMode disabled: Running LoadBalanceRule one time", "ApiService");

                        //If loadbalance is active the battety priority is set
                        await ApiAutoModeDisabledLoadBalanceRule();

                        CurrentState.IsRTMAutoModeRunning = false;
                    }
                }
            }

            await GrowattGetDevice();
            await GrowattGetDeviceNoahInfo();
            await GrowattGetDeviceNoahLastData();
        }

        private async Task TibberSavePrices(IList<TibberPrice> prices)
        {
            var dbContext = ApiGetDbContext();
            var topPrices = prices.OrderByDescending(x => x.Total).Take(10).ToList();
            var avg = topPrices.Average(x => x.Total);

            // Verarbeitung der Ergebnisse
            foreach (var price in prices)
            {
                price.AutoModeRestriction = price.Total > avg;

                Console.WriteLine($"Zeit: {price.StartsAt}, Preis: {price.Total} EUR/kWh");

                //Prüfe ob es denn eintrag schon gibt und falls ja mach ein update

                TibberPrices.Add(price);

                // Prüfe, ob der Datensatz bereits existiert
                var existingDevice = await dbContext.Prices.FirstOrDefaultAsync(x => x.StartsAt == price.StartsAt);
                if (existingDevice == null)
                {
                    // Füge den neuen Datensatz hinzu
                    dbContext.Prices.Add(price);
                }
            }
            await dbContext.SaveChangesAsync(); // Änderungen speichern
        }

        #endregion Private Methods

        #region IObservable

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
                var dbContext = ApiGetDbContext();

                var realTimeMeasurementExtention = new RealTimeMeasurementExtention(value);

                await TibberRTMCalcAvgOfLastSeconds(realTimeMeasurementExtention);

                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1, Key = "AvgPowerConsumption", Value = realTimeMeasurementExtention.AvgPowerConsumption.ToString() });
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 2, Key = "AvgPowerProduction", Value = realTimeMeasurementExtention.AvgPowerProduction.ToString() });

                lock (TibberRealTimeMeasurement._syncRoot)
                {
                    TibberRealTimeMeasurement.Add(realTimeMeasurementExtention);
                }

                dbContext.RealTimeMeasurements.Add(realTimeMeasurementExtention); // Speichern in der Datenbank

                await dbContext.SaveChangesAsync(); // Änderungen speichern

                await TibberRTMProceeding(realTimeMeasurementExtention);

                if (realTimeMeasurementExtention.RequestedPowerValue.HasValue)
                    Logger.LogDebug($"RequestedPowerValue: {realTimeMeasurementExtention.RequestedPowerValue}");

                realTimeMeasurementExtention.PenaltyFrequentlyAccess = GrowattDeviceQueryQueueWatchdog.PenaltyFrequentlyAccess;
                realTimeMeasurementExtention.SettingPowerLoadSeconds = ApiSettingAvgPowerLoadSeconds;
                realTimeMeasurementExtention.SettingOffSetAvg = ApiSettingAvgPowerOffset;
                realTimeMeasurementExtention.SettingAvgPowerHysteresis = ApiSettingAvgPowerHysteresis;

                await dbContext.SaveChangesAsync(); // Änderungen speichern
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
            }
        }

        #endregion IObservable
    }
}
