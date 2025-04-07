using BlazorBootstrap;
using EnergyAutomate.Definitions;
using EnergyAutomate.Extentions;
using EnergyAutomate.Tibber;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EnergyAutomate.Services
{
    public partial class ApiService : IObserver<RealTimeMeasurement>
    {
        #region Fields

        private readonly Lock lockAdjustPower = new();
        private readonly Lock lockLoadBalance = new();

        #endregion Fields

        #region Public Constructors

        public ApiService(IServiceProvider serviceProvider)
        {
            CurrentState = new ApiState(serviceProvider, this);
            ServiceProvider = serviceProvider;
            GrowattDeviceQueryQueueWatchdog.OnItemDequeued += GrowattDeviceQueryQueueWatchdog_OnItemDequeued;
        }

        #endregion Public Constructors

        #region Events

        public event EventHandler? StateHasChanged;

        #endregion Events

        #region Properties

        public bool ApiSettingAutoMode { get; set; }
        public int ApiSettingAvgPower { get; set; } = 200;
        public List<APiTraceValue> ApiSettingAvgPowerAdjustmentTraceValues { get; set; } = [];
        public int ApiSettingAvgPowerHysteresis { get; set; } = 50;
        public int ApiSettingAvgPowerLoadSeconds { get; set; } = 0;
        public int ApiSettingAvgPowerOffset { get; set; } = 25;
        public bool ApiSettingBatteryPriorityMode { get; set; } = false;
        public Dictionary<string, int> ApiSettingDataReadsDelaySec { get; set; } = new Dictionary<string, int>() {
            { nameof(DeviceMinInfoDataQuery), 60 * 5 } ,
            { nameof(DeviceMinLastDataQuery), 60 * 5 } ,
            { nameof(DeviceNoahInfoDataQuery), 60 * 5} ,
            { nameof(DeviceNoahLastDataQuery), 60 } ,
            { nameof(DeviceNoahSetPowerQuery), 5 } ,
            { nameof(DeviceNoahTimeSegmentQuery), 5 } ,
            { nameof(DeviceListQuery), 60 }
        };
        public int ApiSettingMaxPower { get; set; } = 840;
        public bool ApiSettingRestrictionMode { get; set; } = false;
        public int ApiSettingTimeOffset { get; set; } = DateTimeOffset.Now.Offset.Hours;
        public ApiState CurrentState { get; set; }
        private ThreadSafeObservableCollection<TibberRealTimeMeasurement> TibberRealTimeMeasurement { get; set; } = [];
        private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog => ServiceProvider.GetRequiredService<ApiRealTimeMeasurementWatchdog>();
        private GrowattApiClient GrowattApiClient => ServiceProvider.GetRequiredService<GrowattApiClient>();
        private List<ApiCallLog> GrowattDataReads { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceMinInfoData> GrowattDeviceMinInfoData { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceMinLastData> GrowattDeviceMinLastData { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceNoahInfoData> GrowattDeviceNoahInfoData { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceNoahLastData> GrowattDeviceNoahLastData { get; set; } = [];
        private ApiQueueWatchdog<IDeviceQuery> GrowattDeviceQueryQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<IDeviceQuery>>();
        private ThreadSafeObservableCollection<DeviceList> GrowattDevices { get; set; } = [];
        private ILogger Logger => ServiceProvider.GetRequiredService<ILogger<ApiService>>();
        private IServiceProvider ServiceProvider { get; set; }
        private Guid? TibberHomeId { get; set; }
        private ThreadSafeObservableCollection<TibberPrice> TibberPrices { get; set; } = [];

        public static IEnumerable<TickMark> GenerateTickTickMarks(int start, int end, int step)
        {
            var tickMarks = new List<TickMark>();
            for (int i = start; i <= end; i += step)
            {
                tickMarks.Add(new TickMark { Label = i.ToString(), Value = i.ToString() });
            }
            return tickMarks;
        }

        #endregion Properties

        #region Public Methods

        public void ApiInvokeStateHasChanged()
        {
            StateHasChanged?.Invoke(this, new EventArgs());
        }

        public async Task ApiLoadDataFromDatabase()
        {
            var dbContext = ApiGetDbContext();

            var devices = await dbContext.GrowattDevices.ToListAsync();
            GrowattDevices.Clear();
            foreach (var device in devices)
            {
                GrowattDevices.Add(device);
            }

            var deviceNoahInfoList = await dbContext.GrowattDeviceNoahInfoData.ToListAsync();
            GrowattDeviceNoahInfoData.Clear();
            foreach (var info in deviceNoahInfoList)
            {
                GrowattDeviceNoahInfoData.Add(info);
            }

            var deviceNoahLastDataList = await dbContext.GrowattDeviceNoahLastData.ToListAsync();
            GrowattDeviceNoahLastData.Clear();
            foreach (var lastData in deviceNoahLastDataList)
            {
                GrowattDeviceNoahLastData.Add(lastData);
            }

            var realTimeMeasurements = await dbContext.TibberRealTimeMeasurements.OrderByDescending(x => x.Timestamp).Take(100).ToListAsync();
            TibberRealTimeMeasurement.Clear();
            foreach (var measurement in realTimeMeasurements)
            {
                TibberRealTimeMeasurement.Add(measurement);
            }

            var prices = await dbContext.TibberPrices.OrderByDescending(x => x.StartsAt).Take(48).ToListAsync();
            TibberPrices.Clear();
            foreach (var price in prices.OrderBy(x => x.StartsAt))
            {
                TibberPrices.Add(price);
            }
        }

        public async Task ApiStartAsync(CancellationToken cancellationToken)
        {
            var tibberClient = ServiceProvider.GetRequiredService<TibberApiClient>();

            var basicData = await tibberClient.GetBasicData(cancellationToken);
            TibberHomeId = basicData.Data.Viewer.Homes.FirstOrDefault()?.Id;

            await ApiLoadDataFromDatabase();
        }

        public async Task ApiStopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        public List<DeviceList> GrowattAllNoahDevices()
        {
            lock (GrowattDevices._syncRoot)
                return GrowattDevices.Where(x => x.DeviceType == "noah").ToList();
        }

        public bool GrowattDataReadsDoRefresh(string queryType, int? delay = null)
        {
            var result = !GrowattDataReads.Where(x => x.MethodeName == queryType && x.TimeStamp > CurrentState.Now.AddSeconds(-(delay ?? ApiSettingDataReadsDelaySec[queryType]))).Any();
            if (result)
            {
                var entry = new ApiCallLog()
                {
                    MethodeName = queryType,
                    TimeStamp = CurrentState.Now,
                    RaisedError = false
                };

                //Add log entry
                GrowattDataReads.Add(entry);
            }

            return result;
        }

        public int GrowattDeviceQueryQueueWatchdogCount()
        {
            return GrowattDeviceQueryQueueWatchdog.Count;
        }

        public int? GrowattGetLastCommitedPowerValue() => GrowattGetLastCommitedPowerValueItem()?.PowerValueNewCommited;

        public TibberRealTimeMeasurement? GrowattGetLastCommitedPowerValueItem()
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                return TibberRealTimeMeasurement.Where(x => x.Timestamp > CurrentState.Now.AddDays(-1) && x.PowerValueNewCommited != null).OrderByDescending(x => x.Timestamp).FirstOrDefault();
            }
        }

        public int? GrowattGetLastRequestedPowerValue() => GrowattGetLastRequestedPowerValueItem()?.PowerValueNewRequested;

        public TibberRealTimeMeasurement? GrowattGetLastRequestedPowerValueItem()
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                return TibberRealTimeMeasurement.Where(x => x.Timestamp > CurrentState.Now.AddDays(-1) && x.PowerValueNewRequested != null).OrderByDescending(x => x.Timestamp).FirstOrDefault();
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

        public DeviceNoahInfoData? GrowattGetNoahInfoDataPerDevice(string deviceSn)
        {
            lock (GrowattDeviceNoahInfoData._syncRoot)
            {
                return GrowattDeviceNoahInfoData.FirstOrDefault(x => x.DeviceSn == deviceSn);
            }
        }

        public List<DeviceNoahInfoData?> GrowattGetNoahInfoDatas()
        {
            List<DeviceNoahInfoData?> result = new List<DeviceNoahInfoData?>();
            lock (GrowattDeviceNoahInfoData._syncRoot)
            {
                foreach (var device in GrowattDevices.Where(x => x.DeviceType == "noah"))
                {
                    var infoData = GrowattDeviceNoahInfoData.Where(x => x.DeviceSn == device.DeviceSn).OrderByDescending(x => x.TS).FirstOrDefault();
                    result.Add(infoData);
                }
            }
            return result;
        }

        public DeviceNoahLastData? GrowattGetNoahLastDataPerDevice(string deviceSn)
        {
            lock (GrowattDeviceNoahLastData._syncRoot)
            {
                var result = GrowattDeviceNoahLastData.Where(x => x.deviceSn == deviceSn).OrderByDescending(x => x.TS).FirstOrDefault();
                return result;
            }
        }

        public List<DeviceNoahLastData?> GrowattGetNoahLastDatas()
        {
            List<DeviceNoahLastData?> result = new List<DeviceNoahLastData?>();
            lock (GrowattDeviceNoahLastData._syncRoot)
            {
                foreach (var device in GrowattDevices.Where(x => x.DeviceType == "noah"))
                {
                    var lastData = GrowattDeviceNoahLastData.Where(x => x.deviceSn == device.DeviceSn).OrderByDescending(x => x.TS).FirstOrDefault();
                    result.Add(lastData);
                }
            }
            return result;
        }

        public async Task GrowattInverterMaxSetPower(int value)
        {
            var device = GrowattDevices.Where(X => X.DeviceType == "min").FirstOrDefault();
            if (device != null)
            {
                var query = new DeviceNoahSetPowerQuery()
                {
                    DeviceType = device.DeviceType,
                    DeviceSn = device.DeviceSn,
                    Value = value,
                    Force = true
                };
                await GrowattApiClient.SetPowerAsync(query);
            }
        }

        public async Task GrowattInvokeBattPriorityDeviceNoah()
        {
            await GrowattSetBattPriorityDeviceNoahTimeSegmentsAsync();
        }

        public async Task GrowattInvokeClearDeviceNoahTimeSegments()
        {
            await GrowattQueryDeviceNoahInfo(true);
            await GrowattClearAllDeviceNoahTimeSegments();
        }

        public async Task GrowattInvokeLoadPriorityDeviceNoah()
        {
            await GrowattQueryLoadPriorityDeviceNoahTimeSegmentsAsync();
        }

        public async Task GrowattInvokeRefreshDeviceList()
        {
            await GrowattQueryDevice(true);
            await GrowattQueryDeviceNoahInfo(true);
            await GrowattQueryDeviceNoahLastData(true);
        }

        public async Task GrowattInvokeRefreshNoahs()
        {
            await GrowattQueryDeviceNoahInfo(true);
            await GrowattQueryDeviceNoahLastData(true);
        }

        public async Task GrowattInvokeRefreshNoahsLastData()
        {
            await GrowattQueryDeviceNoahLastData(true);
        }

        public List<DeviceList> GrowattListDevices()
        {
            lock (GrowattDevices._syncRoot)
                return GrowattDevices.ToList();
        }

        public List<DeviceNoahInfoData> GrowattListNoahInfos()
        {
            lock (GrowattDeviceNoahInfoData._syncRoot)
                return GrowattDeviceNoahInfoData.ToList();
        }

        public List<DeviceNoahLastData> GrowattListNoahLastDatas()
        {
            lock (GrowattDeviceNoahLastData._syncRoot)
                return GrowattDeviceNoahLastData.ToList();
        }

        public List<DeviceList> GrowattOnlineNoahDevices()
        {
            lock (GrowattDevices._syncRoot)
                return GrowattDevices.Where(x => x.DeviceType == "noah" && x.IsOfflineSince == null).ToList();
        }

        public async Task GrowattQueryDevice(bool force = false)
        {
            GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceListQuery() { Force = force });

            await Task.CompletedTask;
        }

        public async Task GrowattQueryDeviceMinInfo(bool force = false)
        {
            if (CurrentState.IsGrowattOnline)
            {
                var deviceSnList = GrowattGetDeviceMinSnList();

                GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceMinInfoDataQuery()
                {
                    Force = false,
                    DeviceType = "Min",
                    DeviceSn = deviceSnList,
                });
            }

            await Task.CompletedTask;
        }

        public async Task GrowattQueryDeviceMinLastData(bool force = false)
        {
            if (CurrentState.IsGrowattOnline)
            {
                var deviceSnList = GrowattGetDeviceMinSnList();

                GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceMinLastDataQuery()
                {
                    DeviceType = "Min",
                    DeviceSn = deviceSnList,
                    Force = force
                });
            }

            await Task.CompletedTask;
        }

        public async Task GrowattQueryDeviceNoahInfo(bool force = false)
        {
            var deviceSnList = GrowattGetDeviceNoahSnList();

            GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahInfoDataQuery()
            {
                Force = false,
                DeviceType = "noah",
                DeviceSn = deviceSnList,
            });

            await Task.CompletedTask;
        }

        public async Task GrowattQueryDeviceNoahLastData(bool force = false)
        {
            if (CurrentState.IsGrowattOnline)
            {
                var deviceSnList = GrowattGetDeviceNoahSnList();

                GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahLastDataQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = deviceSnList,
                    Force = force
                });
            }

            await Task.CompletedTask;
        }

        public async Task GrowattSetElementActive(GrowattElement growattElement)
        {
            var dbContext = ApiGetDbContext();
            var item = await dbContext.GrowattElements.FirstOrDefaultAsync(x => x.Id == growattElement.Id);
            if (item != null)
            {
                item.IsActive = true;

                // Setze alle anderen vom gleichen Typ auf false
                var elementsOfSameType = await dbContext.GrowattElements
                    .Where(x => x.ElementType == growattElement.ElementType && x.Id != growattElement.Id)
                    .ToListAsync();

                foreach (var element in elementsOfSameType)
                {
                    element.IsActive = false;
                }
            }

            await dbContext.SaveChangesAsync();
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

        public List<TibberRealTimeMeasurement> TibberListRealTimeMeasurement()
        {
            lock (TibberRealTimeMeasurement._syncRoot)
                return TibberRealTimeMeasurement.ToList();
        }

        public void TibberRealTimeMeasurementRegisterOnCollectionChanged(object origin, Action callback) => TibberRealTimeMeasurement.RegisterOnCollectionChanged(origin, callback);
        
        #endregion Public Methods

        #region Private Methods

        private static bool GrowattNearofBatterySocEmpty(DeviceNoahLastData deviceNoahLastData)
        {
            return deviceNoahLastData.totalBatteryPackChargingStatus == 0
                ? Math.Abs(deviceNoahLastData.battery1Soc - deviceNoahLastData.dischargeSocLimit) < 2
                : false;
        }

        private static bool GrowattNearofBatterySocFull(DeviceNoahLastData deviceNoahLastData)
        {
            return deviceNoahLastData.totalBatteryPackChargingStatus == 0
                ? Math.Abs(deviceNoahLastData.battery1Soc - deviceNoahLastData.chargeSocLimit) < 2
                : false;
        }

        private async Task ApiAutoModeDisabledLoadBalanceRule()
        {
            // If the automatic mode is disabled, the power value is set to 0
            await GrowattClearSetPowerAsync();

            if (ApiSettingBatteryPriorityMode)
            {
                Logger.LogTrace("LoadBalanced: Set BattPriority");
                //If loadbalance is active the battety priority is set
                await GrowattSetBattPriorityDeviceNoahTimeSegmentsAsync();
            }
            else
            {
                Logger.LogTrace("LoadBalanced: Set LoadPriority");

                //If loadbalance is not active the load priority is set
                // Calc avg power value
                await GrowattQueryLoadPriorityDeviceNoahTimeSegmentsAsync();
            }
        }

        private async Task ApiAutoModeEnabledLoadBalanceRule()
        {
            // If the automatic mode is enabled and the restriction is not active, the power value
            // is set to 0
            await GrowattClearSetPowerAsync();
        }

        private ApplicationDbContext ApiGetDbContext()
        {
            return ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
        }

        private async Task GrowattClearAllDeviceNoahTimeSegments()
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattQueryClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

            GrowattDeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
        }

        private async Task GrowattClearSetPowerAsync(int powerValue = 0)
        {
            var devices = GrowattDevices.Where(x => x.DeviceType == "noah").ToList();

            var powerValuePerDevice = powerValue / devices.Count;

            foreach (var device in devices)
            {
                GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahSetPowerQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = device.DeviceSn,
                    Value = powerValuePerDevice,
                    Force = true
                });
            }

            await Task.CompletedTask;
        }

        private async Task<ApiException?> GrowattDeviceQueryQueueWatchdog_OnItemDequeued(IDeviceQuery item, GrowattApiClient growattApiClient)
        {
            if (item == null)
                return default;

            var dbContext = ApiGetDbContext();

            TibberRealTimeMeasurement? dataRealTimeMeasurementApiService = null;
            TibberRealTimeMeasurement? dataRealTimeMeasurementDbContext = null;
            DeviceList? device = null;

            if 
            (   item is DeviceListQuery ||
                !string.IsNullOrWhiteSpace(item.DeviceType) && !string.IsNullOrWhiteSpace(item.DeviceSn)
            )
            {
                if (item.Force || GrowattDataReadsDoRefresh(item.GetType().Name))
                {
                    switch (item)
                    {
                        case DeviceNoahSetPowerQuery setPowerQuery:

                            device = GrowattDevices.FirstOrDefault(x => x.DeviceSn == setPowerQuery.DeviceSn);
                            if (setPowerQuery.TS.HasValue)
                            {
                                lock (TibberRealTimeMeasurement._syncRoot)
                                    dataRealTimeMeasurementApiService = TibberRealTimeMeasurement.FirstOrDefault(x => x.TS == setPowerQuery.TS);

                                dataRealTimeMeasurementDbContext = dbContext.TibberRealTimeMeasurements.FirstOrDefault(x => x.TS == setPowerQuery.TS);
                            }

                            try
                            {
                                await growattApiClient.SetPowerAsync(item);

                                if (device != null)
                                    lock (GrowattDevices._syncRoot)
                                        device.PowerValueCommited = setPowerQuery.Value;

                                if (dataRealTimeMeasurementApiService != null)
                                    lock (TibberRealTimeMeasurement._syncRoot)
                                        dataRealTimeMeasurementApiService.PowerValueNewCommited = setPowerQuery.Value;

                                if (dataRealTimeMeasurementDbContext != null)
                                {
                                    dataRealTimeMeasurementDbContext.PowerValueNewCommited = setPowerQuery.Value;
                                    await dbContext.SaveChangesAsync();
                                }

                                Logger.LogTrace($"Commited lastRequestedPowerValue: {setPowerQuery.Value} W");

                                ApiInvokeStateHasChanged();

                                return default; // Operation erfolgreich
                            }
                            catch (ApiException ex)
                            {
                                return ex; // Operation fehlgeschlagen
                            }
                        case DeviceNoahTimeSegmentQuery timeSegmentQuery:
                            try
                            {
                                await growattApiClient.SetTimeSegmentAsync(item);
                                return default; // Operation erfolgreich
                            }
                            catch (ApiException ex)
                            {
                                return ex; // Operation fehlgeschlagen
                            }
                        case DeviceNoahLastDataQuery lastDataQuery:
                            try
                            {
                                //Refresh Last data ever minute
                                var deviceNoahLastDatas = await growattApiClient.GetDeviceLastDataAsync<DeviceNoahLastDataResponse>(lastDataQuery);
                                if (deviceNoahLastDatas != null)
                                {
                                    await GrowattSaveDeviceNoahLastData(deviceNoahLastDatas.Data.Noah);
                                }
                                return default; // Operation erfolgreich
                            }
                            catch (ApiException ex)
                            {
                                return ex; // Operation fehlgeschlagen
                            }
                        case DeviceNoahInfoDataQuery infoQuery:
                            try
                            {
                                //Refresh Last data ever minute
                                var deviceNoahInfos = await growattApiClient.GetDeviceInfoAsync<DeviceNoahInfoDataResponse>(infoQuery);
                                if (deviceNoahInfos != null)
                                {
                                    await GrowattSaveDeviceNoahInfoData(deviceNoahInfos.Data.Noah);
                                }

                                return default; // Operation erfolgreich
                            }
                            catch (ApiException ex)
                            {
                                return ex; // Operation fehlgeschlagen
                            }
                        case DeviceMinLastDataQuery lastDataQuery:
                            try
                            {
                                //Refresh Last data ever minute
                                var deviceNoahLastDatas = await growattApiClient.GetDeviceLastDataAsync<DeviceMinLastDataResponse>(lastDataQuery);
                                if (deviceNoahLastDatas != null)
                                {
                                    await GrowattSaveDeviceMinLastData(deviceNoahLastDatas.Data.Min);
                                }
                                return default; // Operation erfolgreich
                            }
                            catch (ApiException ex)
                            {
                                return ex; // Operation fehlgeschlagen
                            }
                        case DeviceMinInfoDataQuery infoQuery:
                            try
                            {
                                //Refresh Last data ever minute
                                var deviceNoahInfos = await growattApiClient.GetDeviceInfoAsync<DeviceMinInfoDataResponse>(infoQuery);
                                if (deviceNoahInfos != null)
                                {
                                    await GrowattSaveDeviceMinInfoData(deviceNoahInfos.Data.Min);
                                }

                                return default; // Operation erfolgreich
                            }
                            catch (ApiException ex)
                            {
                                return ex; // Operation fehlgeschlagen
                            }
                        case DeviceListQuery infoQuery:
                            List<DeviceList>? deviceLists = null;
                            try
                            {
                                deviceLists = await GrowattApiClient.GetDeviceListAsync();
                                if (deviceLists != null)
                                {
                                    await GrowattSaveDeviceList(deviceLists);
                                }
                                return default; // Operation erfolgreich
                            }
                            catch (ApiException ex)
                            {
                                return ex; // Operation fehlgeschlagen
                            }
                        default:
                            return default;
                    }
                }
            }

            return default;
        }

        private string GrowattGetDeviceMinSnList()
        {
            return string.Join(",", GrowattDevices.Where(x => x.DeviceType == "min").Select(x => x.DeviceSn).ToList());
        }

        private string GrowattGetDeviceNoahSnList()
        {
            return string.Join(",", GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());
        }

        private async Task GrowattQueryClearDeviceNoahTimeSegments(Queue<IDeviceQuery> DeviceTimeSegmentQueue)
        {
            var deviceSnList = GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

            foreach (var deviceSn in deviceSnList)
            {
                var data = GrowattDeviceNoahInfoData.FirstOrDefault(x => x.DeviceSn == deviceSn);
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

            await Task.CompletedTask;
        }

        private async Task GrowattQueryLoadPriorityDeviceNoahTimeSegmentsAsync(int powerValue = 0)
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattQueryClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

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

                var existingDevice = await dbContext.GrowattDevices.FindAsync(deviceList.DeviceSn);
                if (existingDevice != null)
                {
                    dbContext.Entry(existingDevice).CurrentValues.SetValues(deviceList);
                }
                else
                {
                    dbContext.GrowattDevices.Add(deviceList);
                }
            }

            await dbContext.SaveChangesAsync();

            ApiInvokeStateHasChanged();
        }

        private async Task GrowattSaveDeviceMinInfoData(List<DeviceMinInfoData> deviceMinInfos)
        {
            var dbContext = ApiGetDbContext();

            foreach (var deviceMinInfo in deviceMinInfos)
            {
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(deviceMinInfo.lastUpdateTime).DateTime;
                var offset = TimeSpan.FromHours(-6); // Beispiel: Offset von 2 Stunden
                deviceMinInfo.TS = new DateTimeOffset(dateTime, offset);

                lock (GrowattDeviceMinInfoData._syncRoot)
                {
                    var apiServiceDeviceMinInfo = GrowattDeviceMinInfoData.FirstOrDefault(x => x.serialNum == deviceMinInfo.serialNum);
                    if (apiServiceDeviceMinInfo != null) GrowattDeviceMinInfoData.Remove(apiServiceDeviceMinInfo);
                    GrowattDeviceMinInfoData.Add(deviceMinInfo);
                }

                var dbContextDeviceMinInfo = await dbContext.GrowattDeviceMinInfoData.FindAsync(deviceMinInfo.serialNum);
                if (dbContextDeviceMinInfo != null)
                {
                    dbContext.Entry(dbContextDeviceMinInfo).CurrentValues.SetValues(deviceMinInfo);
                }
                else
                {
                    dbContext.GrowattDeviceMinInfoData.Add(deviceMinInfo);
                }
            }

            // Save changes to the database
            await dbContext.SaveChangesAsync();

            ApiInvokeStateHasChanged();
        }

        private async Task GrowattSaveDeviceMinLastData(List<DeviceMinLastData> deviceMinLastDatas)
        {
            var dbContext = ApiGetDbContext();

            foreach (var deviceMinLastData in deviceMinLastDatas)
            {
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(deviceMinLastData.Calendar).DateTime;
                var offset = TimeSpan.FromHours(-6); // Beispiel: Offset von 2 Stunden
                deviceMinLastData.TS = new DateTimeOffset(dateTime, offset);

                lock (GrowattDeviceMinLastData._syncRoot)
                {
                    GrowattDeviceMinLastData.Add(deviceMinLastData);
                }

                var dbContextDeviceMinLastData = await dbContext.GrowattDeviceMinLastData.FindAsync(deviceMinLastData.SerialNum, deviceMinLastData.Time);
                if (dbContextDeviceMinLastData != null)
                {
                    dbContext.Entry(dbContextDeviceMinLastData).CurrentValues.SetValues(deviceMinLastData);
                }
                else
                {
                    dbContext.GrowattDeviceMinLastData.Add(deviceMinLastData);
                }
            }

            await dbContext.SaveChangesAsync();

            ApiInvokeStateHasChanged();
        }

        private async Task GrowattSaveDeviceNoahInfoData(List<DeviceNoahInfoData> deviceNoahInfoDatas)
        {
            var dbContext = ApiGetDbContext();

            foreach (var deviceNoahInfo in deviceNoahInfoDatas)
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

                lock (GrowattDeviceNoahInfoData._syncRoot)
                {
                    var apiServiceDeviceNoahInfo = GrowattDeviceNoahInfoData.FirstOrDefault(x => x.DeviceSn == deviceNoahInfo.DeviceSn);
                    if (apiServiceDeviceNoahInfo != null) GrowattDeviceNoahInfoData.Remove(apiServiceDeviceNoahInfo);
                    GrowattDeviceNoahInfoData.Add(deviceNoahInfo);
                }

                var deviceDbContext = dbContext.GrowattDevices.FirstOrDefault(x => x.DeviceType == "noah" && x.DeviceSn == deviceNoahInfo.DeviceSn);
                if (deviceDbContext != null)
                {
                    deviceDbContext.IsOfflineSince = deviceNoahInfo.Lost ? new DateTime(deviceNoahInfo.LastUpdateTime) : null;
                }

                var dbContextDeviceNoahInfo = await dbContext.GrowattDeviceNoahInfoData.FindAsync(deviceNoahInfo.DeviceSn);
                if (dbContextDeviceNoahInfo != null)
                {
                    dbContext.Entry(dbContextDeviceNoahInfo).CurrentValues.SetValues(deviceNoahInfo);
                }
                else
                {
                    dbContext.GrowattDeviceNoahInfoData.Add(deviceNoahInfo);
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

                        device.IsBatteryEmpty = deviceNoahLastData.totalBatteryPackChargingStatus == 0 && GrowattNearofBatterySocEmpty(deviceNoahLastData);
                        device.IsBatteryFull = deviceNoahLastData.totalBatteryPackChargingStatus == 0 && GrowattNearofBatterySocFull(deviceNoahLastData);
                    }
                }

                lock (GrowattDeviceNoahLastData._syncRoot)
                {
                    GrowattDeviceNoahLastData.Add(deviceNoahLastData);
                }

                var deviceDbContext = await dbContext.GrowattDevices.FindAsync(deviceNoahLastData.deviceSn);
                if (deviceDbContext != null)
                {
                    deviceDbContext.PowerValueRequested = (int)deviceNoahLastData.pac;
                    deviceDbContext.PowerValueCommited = (int)deviceNoahLastData.pac;
                }

                var dbContextDeviceNoahLastData = await dbContext.GrowattDeviceNoahLastData.FindAsync(deviceNoahLastData.deviceSn, deviceNoahLastData.time);
                if (dbContextDeviceNoahLastData != null)
                {
                    dbContext.Entry(dbContextDeviceNoahLastData).CurrentValues.SetValues(deviceNoahLastData);
                }
                else
                {
                    dbContext.GrowattDeviceNoahLastData.Add(deviceNoahLastData);
                }
            }

            await dbContext.SaveChangesAsync();

            ApiInvokeStateHasChanged();
        }

        private async Task GrowattSetBattPriorityDeviceNoahTimeSegmentsAsync()
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattQueryClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

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

        private async Task GrowattSetPowerToSolarInputAsync(TibberRealTimeMeasurement value)
        {
            var devices = GrowattDevices.Where(x => x.DeviceType == "noah").ToList();

            foreach (var device in devices)
            {
                var infoData = GrowattGetNoahInfoDataPerDevice(device.DeviceSn);
                var lastData = GrowattGetNoahLastDataPerDevice(device.DeviceSn);
                var lastDataPPV = (int)(lastData?.ppv ?? 0);

                if (infoData != null && lastData != null && infoData.DefaultPower != lastDataPPV && device.PowerValueRequested != lastDataPPV)
                {
                    device.PowerValueRequested = lastDataPPV;
                    device.PowerValueLastChanged = value.TS;
                    device.PowerValueRequested = lastDataPPV;
                    value.PowerValueNewRequested = lastDataPPV;
                    GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahSetPowerQuery()
                    {
                        DeviceType = "noah",
                        DeviceSn = device.DeviceSn,
                        Value = lastDataPPV,
                        Force = true
                    });
                }
            }

            await Task.CompletedTask;
        }

        private async Task TibberRTMAdjustment1(TibberRealTimeMeasurement value)
        {
            if (ApiSettingAutoMode && CurrentState.IsGrowattOnline)
            {
                // If the automatic mode is enabled, the power value is adjusted
                if (!CurrentState.IsRTMAutoModeRunning)
                {
                    //Clear all time segments
                    await GrowattClearAllDeviceNoahTimeSegments();

                    Logger.LogTrace("Not in grace periode: Running LoadBalanceRule one time");
                    //If loadbalance is active the battety priority is set
                    await ApiAutoModeEnabledLoadBalanceRule();
                }

                // If the automatic mode is enabled
                if (ApiSettingRestrictionMode)
                {
                    value.SettingRestrictionState = CurrentState.IsExpensiveRestrictionMode;
                    value.SettingAutoMode = ApiSettingAutoMode;
                    value.SettingRestrictionState = ApiSettingRestrictionMode;
                    value.SettingBatteryPriorityMode = ApiSettingBatteryPriorityMode;

                    // If the automatic mode is enabled and the restriction is active, the power
                    // value is adjusted
                    if (CurrentState.IsExpensiveRestrictionMode)
                    {
                        if (!CurrentState.IsRTMRestrictionModeRunning)
                        {
                            Logger.LogTrace("Are in grace periode: Running SetNoDeviceNoahTimeSegments one time");

                            //Clear all time segments
                            await GrowattClearAllDeviceNoahTimeSegments();
                        }

                        if (ApiSettingBatteryPriorityMode)
                        {
                            await TibberRTMAdjustment1PowerSet(value);
                        }
                        else
                        {
                            await TibberRTMDefaultBatteryPriority();
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

                            Logger.LogTrace("AutoMode enabled, not in grace periode: Running LoadBalanceRule one time");

                            //If loadbalance is active the battety priority is set
                            //await ApiAutoModeDisabledLoadBalanceRule();

                            CurrentState.IsRTMRestrictionModeRunning = false;
                        }

                        Logger.LogTrace("Not in grace periode: Nothing to do");

                        CurrentState.IsRTMRestrictionModeRunning = false;
                    }
                }
                else
                {
                    // If the automatic mode is enabled and the restriction is not active, the power
                    // value is set to 0

                    if (ApiSettingBatteryPriorityMode)
                    {
                        await TibberRTMDefaultBatteryPriority();
                    }
                    else
                    {
                        await TibberRTMAdjustment1PowerSet(value);
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

                        Logger.LogTrace("AutoMode disabled: Running LoadBalanceRule one time");

                        //If loadbalance is active the battety priority is set
                        await ApiAutoModeDisabledLoadBalanceRule();

                        CurrentState.IsRTMAutoModeRunning = false;
                    }
                }
            }

            if (value.PowerValueNewRequested.HasValue)
                Logger.LogDebug($"RequestedPowerValue: {value.PowerValueNewRequested}");

            value.ApiPenaltyFrequentlyAccess = GrowattDeviceQueryQueueWatchdog.PenaltyFrequentlyAccess;
            value.SettingPowerLoadSeconds = ApiSettingAvgPowerLoadSeconds;
            value.SettingOffSetAvg = ApiSettingAvgPowerOffset;
            value.SettingAvgPowerHysteresis = ApiSettingAvgPowerHysteresis;

            await GrowattQueryDevice();
            await GrowattQueryDeviceNoahInfo();
            await GrowattQueryDeviceNoahLastData();
        }

        private async Task TibberRTMAdjustment1PowerCalc(TibberRealTimeMeasurement value)
        {
            int calcPowerValue = 0;
            int newPowerValue = 0;

            var devices = GrowattOnlineNoahDevices();

            DeviceList? device = null;

            var upperlimit = ApiSettingAvgPowerOffset + (ApiSettingAvgPowerHysteresis / 2);
            var lowerlimit = ApiSettingAvgPowerOffset - (ApiSettingAvgPowerHysteresis / 2);

            int consumptionDelta = 0;
            int productionDelta = 0;

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 201, Key = "DeltaPowerValue", Value = productionDelta.ToString() });

            if (value.TotalPower > 0)
                device = devices.OrderBy(x => x.PowerValueCommited).FirstOrDefault();

            if (value.TotalPower < 0)
                device = devices.OrderByDescending(x => x.PowerValueCommited).FirstOrDefault();

            if (device != null)
            {
                int lastCommitedPowerValue = device.PowerValueCommited == 0 ? (int)(GrowattGetNoahLastDataPerDevice(device.DeviceSn)?.pac ?? 0) : device.PowerValueCommited;
                var lastRequestedPowerValue = device.PowerValueRequested;
                var avgPowerConsumption = value.PowerAvgConsumption;
                var avgPowerProduction = -value.PowerAvgProduction;

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
                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 202, Key = "DeltaPowerValue", Value = consumptionDelta.ToString() });
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
                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 203, Key = "DeltaPowerValue", Value = productionDelta.ToString() });
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

                        GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahSetPowerQuery()
                        {
                            DeviceType = "noah",
                            DeviceSn = device.DeviceSn,
                            Value = newPowerValue,
                            TS = value.TS
                        });

                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 204, Key = "NewPowerValue", Value = newPowerValue.ToString() });
                    }
                }
                else
                {
                    if (value.TotalPower > 0)
                    {
                        Logger.LogTrace($"TotalPower: {value.TotalPower}, AvgPowerProduction: {avgPowerConsumption}, upperDelta: {consumptionDelta} = {avgPowerConsumption} - {upperlimit}");
                        Logger.LogTrace($"lastCommitedPowerValue: {lastCommitedPowerValue}, upperDelta: {consumptionDelta} = {avgPowerConsumption} - {upperlimit}, calcPowerValue: {calcPowerValue}, OffSet: {ApiSettingAvgPowerOffset}");
                    }
                    if (value.TotalPower < 0)
                    {
                        Logger.LogTrace($"TotalPower: {value.TotalPower}, AvgPowerProduction: {avgPowerProduction}, lowerDelta: {productionDelta} = {avgPowerProduction} - {lowerlimit}");
                        Logger.LogTrace($"lastCommitedPowerValue: {lastCommitedPowerValue} - lowerDelta: {productionDelta}, calcPowerValue: {calcPowerValue}, OffSet: {ApiSettingAvgPowerOffset}");
                    }
                }
                
                value.PowerValueNewRequested = newPowerValue;
                value.PowerValueNewCommited = 0;
                value.PowerValueNewDeviceSn = device.DeviceSn;

                value.PowerValueTotalCommited = devices.Sum(x => x.PowerValueCommited);
                value.PowerValueTotalRequested = devices.Sum(x => x.PowerValueRequested);

                ApiInvokeStateHasChanged();
            }

            await Task.CompletedTask;
        }

        private async Task TibberRTMAdjustment1PowerSet(TibberRealTimeMeasurement value)
        {
            if (CurrentState.CheckRTMCondition("RTMPowerAdjustment1PowerCalc"))
            {
                Logger.LogInformation($"No solar power, set power to AVG power output value");
                await GrowattClearAllDeviceNoahTimeSegments();
            }
            // If the battery is not full and the restriction mode is not cheap activate zero injection
            await TibberRTMAdjustment1PowerCalc(value);
        }

        private async Task TibberRTMAdjustment2(TibberRealTimeMeasurement value)
        {
            if (CurrentState.GrowattNoahTotalPPV < ApiSettingAvgPower)
            {
                if (CurrentState.IsGrowattBatteryEmpty)
                {
                    Logger.LogInformation($"Battery is empty, set power to 0");
                }
                else
                {
                    if (CurrentState.IsExpensiveRestrictionMode || ApiSettingAutoMode)
                    {
                        await TibberRTMAdjustment2PowerSet(value);
                    }
                    else
                    {
                        // If the battery is not empty and the restriction mode is not expensive
                        // activate avg injection
                        if (CurrentState.CheckRTMCondition($"SetPower_Avg_{ApiSettingAvgPower}"))
                        {
                            Logger.LogInformation($"No solar power, set power to AVG power output value");
                            await GrowattClearAllDeviceNoahTimeSegments();
                            await GrowattClearSetPowerAsync(ApiSettingAvgPower);
                        }
                    }
                }
            }
            else if (CurrentState.GrowattNoahTotalPPV > 840)
            {
                if (CurrentState.IsGrowattBatteryFull)
                {
                    Logger.LogInformation($"Battery is full, no action needed");

                    await TibberRTMDefaultBatteryPriority();
                }
                else
                {
                    //If cloudy
                    if (CurrentState.IsCloudy)
                    {
                        await TibberRTMAdjustment2PowerSet(value);
                        ApiSettingAvgPowerHysteresis = 10;
                        ApiSettingAvgPowerOffset = -25;
                    }
                    else
                    {
                        // If the battery is not full and the restriction mode is cheap load with
                        // full soloar power
                        if (CurrentState.CheckRTMCondition("LoadPriority_SetPower_840"))
                        {
                            await GrowattClearAllDeviceNoahTimeSegments();
                            await GrowattClearSetPowerAsync(840);
                        }
                    }
                }
            }
            else
            {
                if (!CurrentState.IsGrowattBatteryFull)
                {
                    //If cloudy
                    if (CurrentState.IsCloudy)
                    {
                        if (CurrentState.IsExpensiveRestrictionMode || ApiSettingAutoMode)
                        {
                            await TibberRTMAdjustment2PowerSet(value);
                        }
                        else
                        {
                            await TibberRTMDefaultBatteryPriority();
                        }
                    }
                    else
                    {
                        if (CurrentState.IsExpensiveRestrictionMode || !CurrentState.IsBelowAvgPrice || ApiSettingAutoMode)
                        {
                            //Battery is not full and it's not not cloudy and expensive restriction mode so we force load
                            await TibberRTMAdjustment2PowerSet(value);
                        }
                        else
                        {
                            // If the price is not expensive and below avg price

                            if (CurrentState.CheckRTMCondition("SetPowerToSolarInput"))
                            {
                                await GrowattClearAllDeviceNoahTimeSegments();
                            }
                            await GrowattSetPowerToSolarInputAsync(value);
                        }
                    }
                }
                else
                {
                    if (CurrentState.IsExpensiveRestrictionMode || ApiSettingAutoMode)
                    {
                        await TibberRTMAdjustment2PowerSet(value);
                    }
                    else
                    {
                        await TibberRTMDefaultBatteryPriority();
                    }
                }
            }

            if (value.PowerValueNewRequested.HasValue)
                Logger.LogDebug($"RequestedPowerValue: {value.PowerValueNewRequested}");

            value.ApiPenaltyFrequentlyAccess = GrowattDeviceQueryQueueWatchdog.PenaltyFrequentlyAccess;
            value.SettingPowerLoadSeconds = ApiSettingAvgPowerLoadSeconds;
            value.SettingOffSetAvg = ApiSettingAvgPowerOffset;
            value.SettingAvgPowerHysteresis = ApiSettingAvgPowerHysteresis;

            await GrowattQueryDevice();
            await GrowattQueryDeviceNoahInfo();
            await GrowattQueryDeviceNoahLastData();
        }

        private async Task TibberRTMAdjustment2PowerCalc(TibberRealTimeMeasurement value)
        {
            var devices = GrowattOnlineNoahDevices();
            var totalCommited = devices.Sum(x => x.PowerValueCommited);

            int calcPowerValue = 0;
            int newPowerValue = 0;

            DeviceList? device = null;

            var upperlimit = ApiSettingAvgPowerOffset + (ApiSettingAvgPowerHysteresis / 2);
            var lowerlimit = ApiSettingAvgPowerOffset - (ApiSettingAvgPowerHysteresis / 2);

            int consumptionDelta = 0;
            int productionDelta = 0;

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 201, Key = "DeltaPowerValue", Value = productionDelta.ToString() });

            if (value.TotalPower > 0)
                device = devices.OrderBy(x => x.PowerValueCommited).FirstOrDefault();

            if (value.TotalPower < 0)
                device = devices.OrderByDescending(x => x.PowerValueCommited).FirstOrDefault();

            if (device != null)
            {
                int lastCommitedPowerValue = device.PowerValueCommited == 0 ? (int)(GrowattGetNoahLastDataPerDevice(device.DeviceSn)?.pac ?? 0) : device.PowerValueCommited;
                var lastRequestedPowerValue = device.PowerValueRequested;
                var avgPowerConsumption = value.PowerAvgConsumption;
                var avgPowerProduction = -value.PowerAvgProduction;

                // If the total power is greater than 0, it indicates power consumption
                if (value.TotalPower > 0 && value.TotalPower > upperlimit)
                {
                    consumptionDelta = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);

                    calcPowerValue = device.PowerValueCommited + consumptionDelta / 2;
                }
                // If the total power is less than 0, it indicates power production
                else if (value.TotalPower < 0 && value.TotalPower < lowerlimit)
                {
                    productionDelta = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);
                    calcPowerValue = device.PowerValueCommited - productionDelta / 2;
                }

                var maxPower = ApiSettingMaxPower / devices.Count;

                newPowerValue = calcPowerValue > maxPower ? maxPower : calcPowerValue < 0 ? 0 : calcPowerValue;

                if (newPowerValue <= maxPower && newPowerValue > 0)
                {
                    if (newPowerValue != lastRequestedPowerValue)
                    {
                        device.PowerValueLastChanged = value.TS;
                        device.PowerValueRequested = newPowerValue;

                        GrowattDeviceQueryQueueWatchdog.Enqueue(new DeviceNoahSetPowerQuery()
                        {
                            DeviceType = "noah",
                            DeviceSn = device.DeviceSn,
                            Value = newPowerValue,
                            TS = value.TS
                        });

                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 202, Key = "NewPowerValue", Value = newPowerValue.ToString() });
                    }
                }
                else
                {
                    if (value.TotalPower > 0)
                    {
                        Logger.LogTrace($"TotalPower: {value.TotalPower}, AvgPowerProduction: {avgPowerConsumption}, upperDelta: {consumptionDelta} = {avgPowerConsumption} - {upperlimit}");
                        Logger.LogTrace($"lastCommitedPowerValue: {lastCommitedPowerValue}, upperDelta: {consumptionDelta} = {avgPowerConsumption} - {upperlimit}, calcPowerValue: {calcPowerValue}, OffSet: {ApiSettingAvgPowerOffset}");
                    }
                    if (value.TotalPower < 0)
                    {
                        Logger.LogTrace($"TotalPower: {value.TotalPower}, AvgPowerProduction: {avgPowerProduction}, lowerDelta: {productionDelta} = {avgPowerProduction} - {lowerlimit}");
                        Logger.LogTrace($"lastCommitedPowerValue: {lastCommitedPowerValue} - lowerDelta: {productionDelta}, calcPowerValue: {calcPowerValue}, OffSet: {ApiSettingAvgPowerOffset}");
                    }
                }

                value.PowerValueNewRequested = newPowerValue;
                value.PowerValueNewCommited = 0;
                value.PowerValueNewDeviceSn = device.DeviceSn;

                value.PowerValueTotalCommited = devices.Sum(x => x.PowerValueCommited);
                value.PowerValueTotalRequested = devices.Sum(x => x.PowerValueRequested);

                ApiInvokeStateHasChanged();
            }

            await Task.CompletedTask;
        }

        private async Task TibberRTMAdjustment2PowerSet(TibberRealTimeMeasurement value)
        {
            if (CurrentState.CheckRTMCondition("RTMPowerAdjustment2PowerCalc"))
            {
                Logger.LogInformation($"No solar power, set power to AVG power output value");
                await GrowattClearAllDeviceNoahTimeSegments();
            }
            // If the battery is not full and the restriction mode is not cheap activate zero injection
            await TibberRTMAdjustment2PowerCalc(value);
        }

        private async Task TibberRTMCalculation1(TibberRealTimeMeasurement value)
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                var measurementsQuery = TibberRealTimeMeasurement;

                if (ApiSettingAvgPowerLoadSeconds > 0)
                    measurementsQuery.Where(m => m.Timestamp >= CurrentState.Now.AddSeconds(-ApiSettingAvgPowerLoadSeconds));

                var measurements = measurementsQuery.ToList();

                measurements.Add(value);

                var powerConsumptionCompleteMeasurements = measurements.OrderByDescending(m => m.Timestamp).ToList().GetEnumerator();

                List<TibberRealTimeMeasurement> powerConsumptionUntilZero = [];

                while (powerConsumptionCompleteMeasurements.MoveNext())
                {
                    if (powerConsumptionCompleteMeasurements.Current.Power == 0) break;
                    powerConsumptionUntilZero.Add(powerConsumptionCompleteMeasurements.Current);
                }

                value.PowerAvgConsumption = value.Power > 0 ? (int)powerConsumptionUntilZero.Average(m => m.Power) : 0;

                var powerProductionCompleteMeasurements = measurements.OrderByDescending(m => m.Timestamp).ToList().GetEnumerator();

                List<TibberRealTimeMeasurement> powerProductionUntilZero = [];

                while (powerProductionCompleteMeasurements.MoveNext())
                {
                    if (powerProductionCompleteMeasurements.Current.PowerProduction == 0) break;
                    powerProductionUntilZero.Add(powerProductionCompleteMeasurements.Current);
                }

                value.PowerAvgProduction = value.PowerProduction > 0 ? (int)powerProductionUntilZero.Average(m => m.PowerProduction ?? 0) : 0;
            }

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 101, Key = "AvgPowerConsumption", Value = value.PowerAvgConsumption.ToString() });
            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 102, Key = "AvgPowerProduction", Value = value.PowerAvgProduction.ToString() });

            await Task.CompletedTask;
        }

        private async Task TibberRTMCalculation2(TibberRealTimeMeasurement value)
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                var measurementsQuery = TibberRealTimeMeasurement;

                if (ApiSettingAvgPowerLoadSeconds > 0)
                    measurementsQuery.Where(m => m.Timestamp >= CurrentState.Now.AddSeconds(-ApiSettingAvgPowerLoadSeconds));

                var measurements = measurementsQuery.ToList();

                measurements.Add(value);

                var powerConsumptionCompleteMeasurements = measurements.OrderByDescending(m => m.Timestamp).ToList().GetEnumerator();

                List<TibberRealTimeMeasurement> powerConsumptionUntilZero = [];

                while (powerConsumptionCompleteMeasurements.MoveNext())
                {
                    if (powerConsumptionCompleteMeasurements.Current.Power == 0) break;
                    powerConsumptionUntilZero.Add(powerConsumptionCompleteMeasurements.Current);
                }

                value.PowerAvgConsumption = value.Power > 0 ? (int)powerConsumptionUntilZero.Average(m => m.Power) : 0;

                var powerProductionCompleteMeasurements = measurements.OrderByDescending(m => m.Timestamp).ToList().GetEnumerator();

                List<TibberRealTimeMeasurement> powerProductionUntilZero = [];

                while (powerProductionCompleteMeasurements.MoveNext())
                {
                    if (powerProductionCompleteMeasurements.Current.PowerProduction == 0) break;
                    powerProductionUntilZero.Add(powerProductionCompleteMeasurements.Current);
                }

                value.PowerAvgProduction = value.PowerProduction > 0 ? (int)powerProductionUntilZero.Average(m => m.PowerProduction ?? 0) : 0;
            }

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 101, Key = "AvgPowerConsumption", Value = value.PowerAvgConsumption.ToString() });
            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 201, Key = "AvgPowerProduction", Value = value.PowerAvgProduction.ToString() });

            await Task.CompletedTask;
        }

        private async Task TibberRTMDefaultBatteryPriority()
        {
            if (CurrentState.CheckRTMCondition("DefaultBatteryPriority_SetPower_0"))
            {
                // If the battery is full and the restriction mode is not expensive activate battery
                // priority and use only surplus power
                await GrowattSetBattPriorityDeviceNoahTimeSegmentsAsync();
                await GrowattClearSetPowerAsync(0);
            }
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
                if (!TibberPrices.Any(x => x.StartsAt == price.StartsAt))
                {
                    TibberPrices.Add(price);
                }

                // Prüfe, ob der Datensatz bereits existiert
                if (await dbContext.TibberPrices.AnyAsync(x => x.StartsAt == price.StartsAt))
                {
                    // Füge den neuen Datensatz hinzu
                    dbContext.TibberPrices.Add(price);
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
                if (!TibberPrices.Any() || (CurrentState.Now.Hour > 13 && CurrentState.Now.Date.AddDays(1) != TibberPrices.Max(x => x.StartsAt).Date))
                {
                    if (CurrentState.CheckRTMCondition($"GetTomorrowPrices_{CurrentState.Now.Hour}"))
                    {
                        await TibberGetTomorrowPrices();
                    }
                }

                var firstTime = CurrentState.WeatherForecast?.Hourly?.Time?.FirstOrDefault();

                if (firstTime == null || firstTime != null && DateTime.Parse(firstTime).Date != CurrentState.Now.Date)
                {
                    CurrentState.WeatherForecast = await CurrentState.GetWeatherForecastAsync();
                }

                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1, Key = "GrowattNoahTotalPPV", Value = CurrentState.GrowattNoahTotalPPV.ToString() });
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 2, Key = "IsCloudy", Value = CurrentState.IsCloudy.ToString() });

                var dbContext = ApiGetDbContext();

                var realTimeMeasurementExtention = new TibberRealTimeMeasurement(value);

                var calculationElement = dbContext.GrowattElements.FirstOrDefault(x => x.ElementType == GrowattElement.ElementTypes.Calculation && x.IsActive);
                if (calculationElement != null)
                {
                    if (calculationElement.Id == GrowattElements.Calculation1.Id)
                    {
                        await TibberRTMCalculation1(realTimeMeasurementExtention);
                    }
                    else if (calculationElement.Id == GrowattElements.Calculation2.Id)
                    {
                        await TibberRTMCalculation2(realTimeMeasurementExtention);
                    }
                }

                lock (TibberRealTimeMeasurement._syncRoot)
                {
                    TibberRealTimeMeasurement.Add(realTimeMeasurementExtention);
                }

                dbContext.TibberRealTimeMeasurements.Add(realTimeMeasurementExtention); // Speichern in der Datenbank

                await dbContext.SaveChangesAsync(); // Änderungen speichern

                var ajustmentElement = dbContext.GrowattElements.FirstOrDefault(x => x.ElementType == GrowattElement.ElementTypes.Adjustment && x.IsActive);
                if (ajustmentElement != null)
                {
                    if (ajustmentElement.Id == GrowattElements.Adjustment1.Id)
                    {
                        await TibberRTMAdjustment1(realTimeMeasurementExtention);
                    }
                    else if (ajustmentElement.Id == GrowattElements.Adjustment2.Id)
                    {
                        await TibberRTMAdjustment2(realTimeMeasurementExtention);
                    }
                }

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
