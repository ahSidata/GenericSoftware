using BlazorBootstrap;
using BlazorMonaco.Bridge;
using EnergyAutomate.Definitions;
using EnergyAutomate.Extentions;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading.Tasks;

namespace EnergyAutomate.Services
{
    public partial class ApiService
    {
        #region Fields

        private readonly Lock lockAdjustPower = new();
        private readonly Lock lockLoadBalance = new();

        private readonly string messageTemplateCommited = "{CurrentState.UtcNow} Commited ({device}) PowerValue: {powerValue} W";
        private readonly string messageTemplateRequested = "{CurrentState.UtcNow} Requested ({device}) PowerValue: {powerValue} W";

        private int _adjustmentWaitCycles = 0;

        #endregion Fields

        #region Public Constructors

        public ApiService(IServiceProvider serviceProvider)
        {
            CurrentState = new ApiState(serviceProvider, this);
            ServiceProvider = serviceProvider;
            GrowattDeviceQueryQueueWatchdog.OnItemDequeued += GrowattDeviceQueryQueueWatchdog_OnItemDequeued;
            Timer = new Timer(TimerCallback, null, 1000, 1000);
        }

        #endregion Public Constructors

        #region Events

        public event EventHandler? StateHasChanged;

        #endregion Events

        #region Properties

        public bool ApiSettingAutoMode { get; set; }
        public int ApiSettingAvgPower { get; set; } = 200;
        public List<APiTraceValue> ApiSettingAvgPowerAdjustmentTraceValues { get; set; } = [];
        public int ApiSettingAvgPowerHysteresis { get; set; } = 40;
        public int ApiSettingAvgPowerLoadSeconds { get; set; } = 0;
        public int ApiSettingAvgPowerOffset { get; set; } = 25;
        public bool ApiSettingBatteryPriorityMode { get; set; } = false;
        public int ApiSettingMaxPower { get; set; } = 840;
        public int ApiSettingPowerAdjustmentFactor { get; set; } = 100;
        public int ApiSettingPowerAdjustmentWaitCycles { get; set; } = 3;
        public bool ApiSettingRestrictionMode { get; set; } = false;
        public int ApiSettingTimeOffset { get; set; } = DateTimeOffset.Now.Offset.Hours;
        public ApiState CurrentState { get; set; }
        private ApiRealTimeMeasurementWatchdog ApiRealTimeMeasurementWatchdog => ServiceProvider.GetRequiredService<ApiRealTimeMeasurementWatchdog>();
        private GrowattApiClient GrowattApiClient => ServiceProvider.GetRequiredService<GrowattApiClient>();
        private ThreadSafeObservableCollection<DeviceMinInfoData> GrowattDeviceMinInfoData { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceMinLastData> GrowattDeviceMinLastData { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceNoahInfoData> GrowattDeviceNoahInfoData { get; set; } = [];
        private ThreadSafeObservableCollection<DeviceNoahLastData> GrowattDeviceNoahLastData { get; set; } = [];
        private ApiQueueWatchdog<IDeviceQuery> GrowattDeviceQueryQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<IDeviceQuery>>();
        private ThreadSafeObservableCollection<DeviceList> GrowattDevices { get; set; } = [];

        private ILogger Logger => ServiceProvider.GetRequiredService<ILogger<ApiService>>();

        private ILogger LoggerRTM => ServiceProvider.GetRequiredService<ILogger<RealTimeMeasurement>>();

        private IServiceProvider ServiceProvider { get; set; }
        private Guid? TibberHomeId { get; set; }
        private ThreadSafeObservableCollection<TibberPrice> TibberPrices { get; set; } = [];
        private ThreadSafeObservableCollection<TibberRealTimeMeasurement> TibberRealTimeMeasurement { get; set; } = [];

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

            var realTimeMeasurements = await dbContext.TibberRealTimeMeasurements.OrderByDescending(x => x.TS).Take(100).ToListAsync();
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
            var basicData = await tibberClient.GetHomes(cancellationToken);
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

        public int GrowattDeviceQueryQueueWatchdogCount()
        {
            return GrowattDeviceQueryQueueWatchdog.Count;
        }

        public List<DeviceList> GrowattGetDeviceLists()
        {
            lock (GrowattDevices._syncRoot)
                return GrowattDevices.ToList();
        }

        public List<DeviceList> GrowattGetDevicesNoahOnline()
        {
            lock (GrowattDevices._syncRoot)
                return GrowattDevices.Where(x => x.DeviceType == "noah" && x.IsOfflineSince == null).ToList();
        }

        public DeviceNoahInfoData? GrowattGetNoahInfoDataPerDevice(string deviceSn)
        {
            lock (GrowattDeviceNoahInfoData._syncRoot)
            {
                return GrowattDeviceNoahInfoData.FirstOrDefault(x => x.DeviceSn == deviceSn);
            }
        }

        public List<DeviceNoahInfoData> GrowattGetNoahInfoDatas()
        {
            lock (GrowattDeviceNoahInfoData._syncRoot)
            {
                return GrowattDeviceNoahInfoData.ToList();
            }
        }

        public DeviceNoahLastData? GrowattGetNoahLastDataPerDevice(string deviceSn)
        {
            lock (GrowattDeviceNoahLastData._syncRoot)
            {
                var result = GrowattDeviceNoahLastData.Where(x => x.deviceSn == deviceSn).OrderByDescending(x => x.TS).FirstOrDefault();
                return result;
            }
        }

        public List<DeviceNoahLastData> GrowattGetNoahLastDatas()
        {
            lock (GrowattDeviceNoahLastData._syncRoot)
            {
                return GrowattDeviceNoahLastData.ToList();
            }
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
                await GrowattApiClient.ExecuteDeviceQueryAsync(query);
            }
        }

        public async Task GrowattInvokeBattPriorityDeviceNoah()
        {
            await GrowattQueryBattPriorityDeviceNoahTimeSegmentsAsync();
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

        public List<DeviceNoahInfoData?> GrowattLatestNoahInfoDatas()
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

        public List<DeviceNoahLastData?> GrowattLatestNoahLastDatas()
        {
            List<DeviceNoahLastData?> result = new List<DeviceNoahLastData?>();
            lock (GrowattDeviceNoahLastData._syncRoot)
            {
                foreach (var device in GrowattDevices.Where(x => x.DeviceType == "noah"))
                {
                    lock (GrowattDeviceNoahLastData._syncRoot)
                    {
                        var lastData = GrowattDeviceNoahLastData.Where(x => x.deviceSn == device.DeviceSn).OrderByDescending(x => x.TS).FirstOrDefault();
                        result.Add(lastData);
                    }
                }
            }
            return result;
        }

        public async Task GrowattQueryDevice(bool force = false)
        {
            var item = new DeviceListQuery() { Force = force };
            if (GrowattDeviceQueryQueueWatchdog.CheckLimits(item))
                GrowattDeviceQueryQueueWatchdog.Enqueue(item);

            await Task.CompletedTask;
        }

        public async Task GrowattQueryDeviceMinInfo(bool force = false)
        {
            if (CurrentState.IsGrowattOnline)
            {
                var deviceSnList = GrowattGetDeviceMinSnList();

                var item = new DeviceMinInfoDataQuery()
                {
                    Force = false,
                    DeviceType = "Min",
                    DeviceSn = deviceSnList,
                };
                if (GrowattDeviceQueryQueueWatchdog.CheckLimits(item))
                    GrowattDeviceQueryQueueWatchdog.Enqueue(item);
            }

            await Task.CompletedTask;
        }

        public async Task GrowattQueryDeviceMinLastData(bool force = false)
        {
            if (CurrentState.IsGrowattOnline)
            {
                var deviceSnList = GrowattGetDeviceMinSnList();

                var item = new DeviceMinLastDataQuery()
                {
                    DeviceType = "Min",
                    DeviceSn = deviceSnList,
                    Force = force
                };
                if (GrowattDeviceQueryQueueWatchdog.CheckLimits(item))
                    GrowattDeviceQueryQueueWatchdog.Enqueue(item);
            }

            await Task.CompletedTask;
        }

        public async Task GrowattQueryDeviceNoahInfo(bool force = false)
        {
            var deviceSnList = GrowattGetDeviceNoahSnList();

            var item = new DeviceNoahInfoDataQuery()
            {
                Force = false,
                DeviceType = "noah",
                DeviceSn = deviceSnList,
            };
            if (GrowattDeviceQueryQueueWatchdog.CheckLimits(item))
                GrowattDeviceQueryQueueWatchdog.Enqueue(item);

            await Task.CompletedTask;
        }

        public async Task GrowattQueryDeviceNoahLastData(bool force = false)
        {
            if (CurrentState.IsGrowattOnline)
            {
                var deviceSnList = GrowattGetDeviceNoahSnList();

                var item = new DeviceNoahLastDataQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = deviceSnList,
                    Force = force
                };
                if (GrowattDeviceQueryQueueWatchdog.CheckLimits(item))
                    GrowattDeviceQueryQueueWatchdog.Enqueue(item);
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

                    var tomorrowList = result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Tomorrow.Select(x => new TibberPrice()
                    {
                        StartsAt = DateTimeOffset.Parse(x.StartsAt).ToUniversalTime(),
                        Total = x.Total,
                        Level = x.Level
                    }).ToList();
                    await TibberSavePrices(tomorrowList);

                    var todayList = result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Today.Select(x => new TibberPrice()
                    {
                        StartsAt = DateTimeOffset.Parse(x.StartsAt).ToUniversalTime(),
                        Total = x.Total,
                        Level = x.Level
                    }).ToList();
                    await TibberSavePrices(todayList);
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

        public void TibberRealTimeMeasurementRegisterOnCollectionChanged(object sender, Action callback) => TibberRealTimeMeasurement.RegisterOnCollectionChanged(sender, callback);

        public void TibberRealTimeMeasurementUnRegisterOnCollectionChanged(object sender) => TibberRealTimeMeasurement.UnRegisterOnCollectionChanged(sender);

        #endregion Public Methods

        #region Private Methods

        private static bool GrowattNearofBatterySocEmpty(DeviceNoahLastData deviceNoahLastData)
        {
            return deviceNoahLastData.totalBatteryPackChargingStatus == 0
                ? Math.Abs(deviceNoahLastData.totalBatteryPackSoc - deviceNoahLastData.dischargeSocLimit) == 0
                : false;
        }

        private static bool GrowattNearofBatterySocFull(DeviceNoahLastData deviceNoahLastData)
        {
            return deviceNoahLastData.totalBatteryPackChargingStatus == 0
                ? Math.Abs(deviceNoahLastData.totalBatteryPackSoc - deviceNoahLastData.chargeSocLimit) < 6
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
                await GrowattQueryBattPriorityDeviceNoahTimeSegmentsAsync();
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

        private async Task<ApiException?> ExecuteWithExceptionHandlingAsync(IDeviceQuery item, DeviceList? device, Func<Task> action)
        {
            try
            {
                await action();
                return null; // Operation erfolgreich
            }
            catch (ApiException ex)
            {
                if (ex.ErrorCode == 5 && device != null)
                {
                    GrowattSetOfflineState(this.ApiGetDbContext(), device.DeviceSn, CurrentState.UtcNow);
                    item.Force = false;
                }
                return ex; // Operation fehlgeschlagen
            }
            catch (Exception ex)
            {
                throw new ApiException("Exception", 1, ex);
            }
        }

        private async Task GrowattBatteryPrioritySetPowerToSolarInputAsync(TibberRealTimeMeasurement value)
        {
            if (CurrentState.CheckRTMCondition("BatteryPriority_SetPower_SolarInput"))
            {
                await GrowattClearAllDeviceNoahTimeSegments();
                await GrowattClearSetPowerAsync(value.TS, 0);
            }

            var devices = GrowattDevices.Where(x => x.DeviceType == "noah").ToList();

            foreach (var device in devices)
            {
                var infoData = GrowattGetNoahInfoDataPerDevice(device.DeviceSn);
                var lastData = GrowattGetNoahLastDataPerDevice(device.DeviceSn);
                var lastDataPPV = (int)(lastData?.ppv - 75 ?? 0);

                if (infoData != null && lastData != null && infoData.DefaultPower != lastDataPPV && device.PowerValueRequested != lastDataPPV)
                {
                    GrowattEnqueuePowerValue(device, lastDataPPV, value.TS);
                }
            }

            await Task.CompletedTask;
        }

        private async Task GrowattClearAllDeviceNoahTimeSegments()
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattQueryClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

            GrowattDeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
        }

        private async Task GrowattClearSetPowerAsync(DateTimeOffset? ts = null, int powerValue = 0)
        {
            GrowattDeviceQueryQueueWatchdog.Clear();

            var devices = GrowattDevices.Where(x => x.DeviceType == "noah").ToList();

            var powerValuePerDevice = powerValue / devices.Count;

            foreach (var device in devices)
            {
                GrowattEnqueuePowerValue(device, powerValue, ts);
            }

            await Task.CompletedTask;
        }

        private async Task<ApiException?> GrowattDeviceQueryQueueWatchdog_OnItemDequeued(IDeviceQuery item, GrowattApiClient growattApiClient, ILogger logger)
        {
            if (item == null)
                return default;

            var dbContext = ApiGetDbContext();

            TibberRealTimeMeasurement? dataRealTimeMeasurementApiService = null;
            TibberRealTimeMeasurement? dataRealTimeMeasurementDbContext = null;
            DeviceList? device = null;

            if
            (item is DeviceListQuery ||
                !string.IsNullOrWhiteSpace(item.DeviceType) && !string.IsNullOrWhiteSpace(item.DeviceSn)
            )
            {
                switch (item)
                {
                    case DeviceNoahSetLowLimitSocQuery setLowLimitSoc:

                        var apiExceptionSetLowLimitSco = await ExecuteWithExceptionHandlingAsync(item, device, async () =>
                        {
                            await GrowattApiClient.ExecuteDeviceQueryAsync(item);
                        });

                        if (apiExceptionSetLowLimitSco != null)
                            return apiExceptionSetLowLimitSco;

                        logger.LogTrace($"Device {setLowLimitSoc.DeviceType} {setLowLimitSoc.DeviceSn} set LowLimitSoc to: {setLowLimitSoc.Value} W");

                        ApiInvokeStateHasChanged();

                        return apiExceptionSetLowLimitSco;
                    case DeviceNoahSetPowerQuery setPowerQuery:

                        device = GrowattDevices.FirstOrDefault(x => x.DeviceSn == setPowerQuery.DeviceSn);
                        if (setPowerQuery.TS.HasValue)
                        {
                            lock (TibberRealTimeMeasurement._syncRoot)
                                dataRealTimeMeasurementApiService = TibberRealTimeMeasurement.FirstOrDefault(x => x.TS == setPowerQuery.TS);

                            dataRealTimeMeasurementDbContext = dbContext.TibberRealTimeMeasurements.FirstOrDefault(x => x.TS == setPowerQuery.TS);
                        }

                        var apiExceptionSetPower = await ExecuteWithExceptionHandlingAsync(item, device, async () =>
                        {
                            await growattApiClient.ExecuteDeviceQueryAsync(item);

                            LoggerRTM.LogTrace(messageTemplateCommited, CurrentState.UtcNow, device.DeviceSn, setPowerQuery.Value);

                            if (device != null)
                            {
                                lock (GrowattDevices._syncRoot)
                                {
                                    device.PowerValueCommited = setPowerQuery.Value;
                                }
                            }
                        });

                        if (apiExceptionSetPower != null)
                            return apiExceptionSetPower;

                        if (dataRealTimeMeasurementApiService != null)
                        {
                            lock (TibberRealTimeMeasurement._syncRoot)
                            {
                                dataRealTimeMeasurementApiService.PowerValueNewCommited += setPowerQuery.Value;
                            }
                        }

                        if (dataRealTimeMeasurementDbContext != null)
                        {
                            dataRealTimeMeasurementDbContext.PowerValueNewCommited += setPowerQuery.Value;
                            await dbContext.SaveChangesAsync();
                        }

                        ApiInvokeStateHasChanged();

                        return apiExceptionSetPower;
                    case DeviceNoahSetTimeSegmentQuery timeSegmentQuery:
                        var apiExceptionTimeSegment = await ExecuteWithExceptionHandlingAsync(item, device, async () =>
                        {
                            await growattApiClient.ExecuteDeviceQueryAsync(item);
                        });

                        if (apiExceptionTimeSegment != null)
                            return apiExceptionTimeSegment;

                        ApiInvokeStateHasChanged();

                        return apiExceptionTimeSegment;
                    case DeviceNoahLastDataQuery lastDataQuery:
                        return await ExecuteWithExceptionHandlingAsync(item, device, async () =>
                        {
                            //Refresh Last data ever minute
                            var deviceNoahLastDatas = await growattApiClient.GetDeviceLastDataAsync<DeviceNoahLastDataResponse>(lastDataQuery);
                            if (deviceNoahLastDatas != null)
                            {
                                await GrowattSaveDeviceNoahLastData(deviceNoahLastDatas.Data.Noah);
                            }
                        });
                    case DeviceMinLastDataQuery lastDataQuery:
                        return await ExecuteWithExceptionHandlingAsync(item, device, async () =>
                        {
                            //Refresh Last data ever minute
                            var deviceNoahLastDatas = await growattApiClient.GetDeviceLastDataAsync<DeviceMinLastDataResponse>(lastDataQuery);
                            if (deviceNoahLastDatas != null)
                            {
                                await GrowattSaveDeviceMinLastData(deviceNoahLastDatas.Data.Min);
                            }
                        });

                    case DeviceNoahInfoDataQuery infoQuery:
                        return await ExecuteWithExceptionHandlingAsync(item, device, async () =>
                        {
                            //Refresh Last data ever minute
                            var deviceNoahInfos = await growattApiClient.GetDeviceInfoAsync<DeviceNoahInfoDataResponse>(infoQuery);
                            if (deviceNoahInfos != null)
                            {
                                await GrowattSaveDeviceNoahInfoData(deviceNoahInfos.Data.Noah);
                            }
                        });
                    case DeviceMinInfoDataQuery infoQuery:
                        return await ExecuteWithExceptionHandlingAsync(item, device, async () =>
                        {
                            //Refresh Last data ever minute
                            var deviceNoahInfos = await growattApiClient.GetDeviceInfoAsync<DeviceMinInfoDataResponse>(infoQuery);
                            if (deviceNoahInfos != null)
                            {
                                await GrowattSaveDeviceMinInfoData(deviceNoahInfos.Data.Min);
                            }
                        });
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

            return default;
        }

        private void GrowattEnqueuePowerValue(DeviceList device, int powerValue, DateTimeOffset? ts = null)
        {
            var item = new DeviceNoahSetPowerQuery()
            {
                DeviceType = "noah",
                DeviceSn = device.DeviceSn,
                Value = powerValue,
                Force = true,
                TS = ts ?? CurrentState.UtcNow
            };

            device.PowerValueRequested = powerValue;
            device.PowerValueLastChanged = ts;

            LoggerRTM.LogTrace(messageTemplateRequested, CurrentState.UtcNow, device.DeviceSn, device.PowerValueRequested);

            GrowattDeviceQueryQueueWatchdog.Enqueue(item);

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 204, Key = "TotalPowerRequested", Value = powerValue.ToString() });
        }

        private string GrowattGetDeviceMinSnList()
        {
            return string.Join(",", GrowattDevices.Where(x => x.DeviceType == "min").Select(x => x.DeviceSn).ToList());
        }

        private string GrowattGetDeviceNoahSnList()
        {
            return string.Join(",", GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());
        }

        private async Task GrowattQueryBattPriorityDeviceNoahTimeSegmentsAsync()
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattQueryClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue, 1);

            var deviceSnList = GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

            foreach (var deviceSn in deviceSnList)
            {
                DeviceTimeSegmentQueue.Enqueue(new DeviceNoahSetTimeSegmentQuery()
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

            GrowattDeviceQueryQueueWatchdog.Enqueue(DeviceTimeSegmentQueue);
        }

        private async Task GrowattQueryClearDeviceNoahTimeSegments(Queue<IDeviceQuery> DeviceTimeSegmentQueue, int skip = 0)
        {
            var deviceSnList = GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

            foreach (var deviceSn in deviceSnList)
            {
                var data = GrowattDeviceNoahInfoData.FirstOrDefault(x => x.DeviceSn == deviceSn);
                if (data != null)
                {
                    var enabledSegments = data.TimeSegments.Where(x => x.Enable == "1").ToList();

                    int index = 1;

                    foreach (var segment in enabledSegments)
                    {
                        if (index > skip)
                        {
                            var request = new DeviceNoahSetTimeSegmentQuery
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
                        index++;
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
                DeviceTimeSegmentQueue.Enqueue(new DeviceNoahSetTimeSegmentQuery()
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
                deviceMinInfo.TS = new DateTimeOffset(dateTime, offset).ToUniversalTime();

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
                deviceMinLastData.TS = new DateTimeOffset(dateTime, offset).ToUniversalTime();

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
                deviceNoahInfo.TS = new DateTimeOffset(dateTime, offset).ToUniversalTime();

                GrowattSetOfflineState(dbContext, deviceNoahInfo.DeviceSn, deviceNoahInfo.Lost ? new DateTime(deviceNoahInfo.LastUpdateTime) : null);

                lock (GrowattDeviceNoahInfoData._syncRoot)
                {
                    var apiServiceDeviceNoahInfo = GrowattDeviceNoahInfoData.FirstOrDefault(x => x.DeviceSn == deviceNoahInfo.DeviceSn);
                    if (apiServiceDeviceNoahInfo != null) GrowattDeviceNoahInfoData.Remove(apiServiceDeviceNoahInfo);
                    GrowattDeviceNoahInfoData.Add(deviceNoahInfo);
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
                deviceNoahLastData.TS = new DateTimeOffset(dateTime, offset).ToUniversalTime();

                lock (GrowattDevices._syncRoot)
                {
                    var device = GrowattDevices.FirstOrDefault(x => x.DeviceSn == deviceNoahLastData.deviceSn);
                    if (device != null)
                    {
                        device.PowerValueCommited = (int)deviceNoahLastData.pac;

                        LoggerRTM.LogTrace(messageTemplateCommited, CurrentState.UtcNow, device.DeviceSn, device.PowerValueCommited);

                        device.IsBatteryEmpty = deviceNoahLastData.totalBatteryPackChargingStatus == 0 && GrowattNearofBatterySocEmpty(deviceNoahLastData);
                        device.IsBatteryFull = deviceNoahLastData.totalBatteryPackChargingStatus == 0 && GrowattNearofBatterySocFull(deviceNoahLastData);
                        device.Soc = deviceNoahLastData.totalBatteryPackSoc;
                    }
                }

                lock (GrowattDeviceNoahLastData._syncRoot)
                {
                    GrowattDeviceNoahLastData.Add(deviceNoahLastData);
                }

                var deviceDbContext = await dbContext.GrowattDevices.FindAsync(deviceNoahLastData.deviceSn);
                if (deviceDbContext != null)
                {
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

        private void GrowattSetOfflineState(ApplicationDbContext dbContext, string deviceSn, DateTimeOffset? dateTimeOffset)
        {
            lock (GrowattDevices._syncRoot)
            {
                var deviceApiService = GrowattDevices.FirstOrDefault(x => x.DeviceType == "noah" && x.DeviceSn == deviceSn);
                if (deviceApiService != null)
                {
                    deviceApiService.IsOfflineSince = dateTimeOffset;
                }
            }

            var deviceDbContext = dbContext.GrowattDevices.FirstOrDefault(x => x.DeviceType == "noah" && x.DeviceSn == deviceSn);
            if (deviceDbContext != null)
            {
                deviceDbContext.IsOfflineSince = dateTimeOffset;
            }
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
        }

        private async Task TibberRTMAdjustment1PowerSet(TibberRealTimeMeasurement value)
        {
            int calcPowerValue = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);
            int newPowerValue = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);

            var devices = GrowattGetDevicesNoahOnline();

            DeviceList? device = null;

            var upperlimit = ApiSettingAvgPowerOffset + (ApiSettingAvgPowerHysteresis / 2);
            var lowerlimit = ApiSettingAvgPowerOffset - (ApiSettingAvgPowerHysteresis / 2);

            int consumptionDelta = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);
            int productionDelta = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 201, Key = "TotalPowerDelta", Value = productionDelta.ToString() });

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
                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 202, Key = "TotalPowerDelta", Value = consumptionDelta.ToString() });
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
                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 203, Key = "TotalPowerDelta", Value = productionDelta.ToString() });
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
                        GrowattEnqueuePowerValue(device, newPowerValue, value.TS);
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

                ApiInvokeStateHasChanged();
            }

            await Task.CompletedTask;
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
                        if (CurrentState.CheckRTMCondition($"LoadPriority_SetPower_Avg_{ApiSettingAvgPower}"))
                        {
                            Logger.LogInformation($"No solar power, set power to AVG power output value");
                            await GrowattClearAllDeviceNoahTimeSegments();
                            await GrowattClearSetPowerAsync(value.TS, ApiSettingAvgPower);
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
                        await TibberRTMAdjustment3TotalPowerMax(value, GrowattGetDeviceNoahSnList());
                    }
                }
            }
            else
            {
                if (CurrentState.IsGrowattBatteryFull)
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
                            await GrowattBatteryPrioritySetPowerToSolarInputAsync(value);
                        }
                    }
                }
            }
        }

        private async Task TibberRTMAdjustment2PowerSet(TibberRealTimeMeasurement value)
        {
            var devices = GrowattGetDevicesNoahOnline();
            var totalCommited = devices.Sum(x => x.PowerValueCommited);

            int calcPowerValue = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);
            int newPowerValue = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);

            DeviceList? device = null;

            var upperlimit = ApiSettingAvgPowerOffset + (ApiSettingAvgPowerHysteresis / 2);
            var lowerlimit = ApiSettingAvgPowerOffset - (ApiSettingAvgPowerHysteresis / 2);

            int consumptionDelta = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);
            int productionDelta = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 201, Key = "TotalPowerDelta", Value = productionDelta.ToString() });

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
                        GrowattEnqueuePowerValue(device, newPowerValue, value.TS);
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

                ApiInvokeStateHasChanged();
            }

            await Task.CompletedTask;
        }

        private async Task TibberRTMAdjustment3(TibberRealTimeMeasurement value)
        {
            var last5Minutes = CurrentState.GrowattNoahGetAvgPpvLast5Minutes();

            if (last5Minutes < ApiSettingAvgPower + 50)
            {
                if (CurrentState.IsGrowattBatteryEmpty)
                {
                    LoggerRTM.LogInformation($"Battery is empty, set power to 0");
                }
                else
                {
                    if (CurrentState.IsExpensiveRestrictionMode || ApiSettingAutoMode)
                    {
                        await TibberRTMAdjustment3PowerSet(value);
                    }
                    else
                    {
                        // If the battery is not empty and the restriction mode is not expensive
                        // activate avg injection
                        if (CurrentState.CheckRTMCondition($"LoadPriority_SetPower_Avg_{ApiSettingAvgPower}"))
                        {
                            LoggerRTM.LogInformation($"No solar power, set power to AVG power output value");
                            await GrowattClearAllDeviceNoahTimeSegments();
                            await GrowattClearSetPowerAsync(value.TS, ApiSettingAvgPower);
                        }
                    }
                }
            }
            else if (last5Minutes > 840)
            {
                if (CurrentState.IsGrowattBatteryFull)
                {
                    LoggerRTM.LogInformation($"Battery is full, no action needed");

                    await TibberRTMDefaultBatteryPriority();
                }
                else
                {
                    //If cloudy
                    if (CurrentState.IsCloudy)
                    {
                        await TibberRTMAdjustment3PowerSet(value);
                        ApiSettingAvgPowerHysteresis = 10;
                        ApiSettingAvgPowerOffset = -25;
                    }
                    else
                    {
                        if (CurrentState.IsCheapRestrictionMode)
                        {
                            // If the battery is not full and the restriction mode is cheap load
                            // with full soloar power
                            await TibberRTMDefaultBatteryPriority();
                        }
                        else
                        {
                            await TibberRTMAdjustment3TotalPowerMax(value, GrowattGetDeviceNoahSnList());
                        }
                    }
                }
            }
            else
            {
                if (CurrentState.IsGrowattBatteryFull)
                {
                    if (CurrentState.IsExpensiveRestrictionMode || ApiSettingAutoMode)
                    {
                        await TibberRTMAdjustment3PowerSet(value);
                    }
                    else
                    {
                        await TibberRTMDefaultBatteryPriority();
                    }
                }
                else
                {
                    //If cloudy
                    if (CurrentState.IsCloudy)
                    {
                        if (CurrentState.IsExpensiveRestrictionMode || ApiSettingAutoMode)
                        {
                            await TibberRTMAdjustment3PowerSet(value);
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
                            await TibberRTMAdjustment3PowerSet(value);
                        }
                        else
                        {
                            // If the tibber price is not expensive and below avg price we not use
                            // the barttery
                            await GrowattBatteryPrioritySetPowerToSolarInputAsync(value);
                        }
                    }
                }
            }
        }

        private int TibberRTMAdjustment3GetCapacity(DeviceList device, int maxPower, int deltaPower)
        {
            //Need more power
            if (deltaPower > 0)
                return maxPower - device.PowerValueCommited;

            //Have too much power
            if (deltaPower < 0)
                return device.PowerValueCommited;

            return default;
        }

        private async Task TibberRTMAdjustment3PowerSet(TibberRealTimeMeasurement value)
        {
            // If last RTM not commited
            if (_adjustmentWaitCycles < ApiSettingPowerAdjustmentWaitCycles)
            {
                _adjustmentWaitCycles++;

                LoggerRTM.LogTrace($"Wait because WaitCycles : {_adjustmentWaitCycles} < {ApiSettingPowerAdjustmentWaitCycles}");

                value.PowerValueNewRequested = null;
                value.PowerValueNewCommited = null;
                value.PowerValueNewDeviceSn = null;
            }
            else
            {
                _adjustmentWaitCycles = 0;

                int deltaTotalPower = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(value.TS, 201, "TotalPowerDelta", deltaTotalPower.ToString()));

                var upperlimit = ApiSettingAvgPowerOffset + (ApiSettingAvgPowerHysteresis / 2);
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(value.TS, 202, "TotalPowerUpperLimit", upperlimit.ToString()));

                var lowerlimit = ApiSettingAvgPowerOffset - (ApiSettingAvgPowerHysteresis / 2);
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(value.TS, 203, "TotalPowerLowerLimit", lowerlimit.ToString()));

                int? requestedTotalPower = null;

                var powerValueTotalCommited = CurrentState.PowerValueTotalCommited;

                if (value.TotalPower > 0)
                {
                    if (value.TotalPower > upperlimit)
                    {
                        requestedTotalPower = value.PowerValueTotalCommited - deltaTotalPower * ApiSettingPowerAdjustmentFactor / 100;
                    }
                    else if (value.TotalPower < lowerlimit)
                    {
                        requestedTotalPower = value.PowerValueTotalCommited + deltaTotalPower * ApiSettingPowerAdjustmentFactor / 100;
                    }
                }
                else if (value.TotalPower < 0)
                {
                    if (value.TotalPower < lowerlimit)
                    {
                        requestedTotalPower = value.PowerValueTotalCommited - deltaTotalPower * ApiSettingPowerAdjustmentFactor / 100;
                    }
                    else if (value.TotalPower > upperlimit)
                    {
                        requestedTotalPower = value.PowerValueTotalCommited + deltaTotalPower * ApiSettingPowerAdjustmentFactor / 100;
                    }
                }

                if (requestedTotalPower.HasValue)
                {
                    ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(value.TS, 206, "TotalPowerCalc", requestedTotalPower.Value.ToString()));

                    if (requestedTotalPower.Value == 0)
                    {
                        Debugger.Break();
                        LoggerRTM.LogTrace($"TotalPower 0 W => no power adjustment");
                    }
                    else
                    {
                        await TibberRTMAdjustment3TotalPowerBalance(value, requestedTotalPower.Value);
                    }
                }
            }

            ApiInvokeStateHasChanged();

            await Task.CompletedTask;
        }

        private async Task TibberRTMAdjustment3TotalPowerBalance(TibberRealTimeMeasurement value, int requestedTotalPower)
        {
            // Validate the requested total power value against the maximum power limit and the
            // minimum power limit

            var deviceList = GrowattGetDevicesNoahOnline();

            var maxPowerPerDevice = ApiSettingMaxPower / deviceList.Count;

            var totalPower = value.TotalPower;

            var deltaTotalPower = value.PowerValueTotalCommited - requestedTotalPower;

            var devices = deltaTotalPower switch
            {
                > 0 => deviceList.OrderByDescending(o => o.Soc).ThenBy(x => x.PowerValueCommited).ToList(), // add delta to the device with the minimum power
                < 0 => deviceList.OrderBy(o => o.Soc).ThenByDescending(x => x.PowerValueCommited).ToList(), // subtract delta from the device with the maximum power
                _ => null // default case, falls TotalPower == 0
            };

            var deviceUtilization = devices?.Select(s => new
            {
                Device = s,
                Capacity = TibberRTMAdjustment3GetCapacity(s, maxPowerPerDevice, deltaTotalPower)
            });

            if (deviceUtilization == null || !deviceUtilization.Any())
            {
                LoggerRTM.LogTrace($"No devices found for power adjustment");
                return;
            }

            // Sample MaxPowerPerDevice = 420 TotalPower = 1000 Device1 = 220 Device2 = 300
            // CapacityDevice1 = 420 - 220 = 200 CapacityDevice2 = 420 - 300 = 120
            // CapacityDevice1 = 0 - 220 = -220 CapacityDevice2 =  0 - 300 = -300
            // CapacityDevice1 = 220 - 220 = 0 CapacityDevice2 = 420 - 300 = 120

            var utilization = deviceUtilization.Sum(s => s.Capacity);

            // Check if the total power exceeds the sum of device capacities or utilization is min or max
            var absTotalPower = Math.Abs(totalPower);

            // Check if the total power exceeds the sum of device capacities or utilization is min or max

            var singleDevice = deviceUtilization.FirstOrDefault(dc => dc.Capacity >= Math.Abs(deltaTotalPower));
            if (singleDevice != null)
            {
                if (CurrentState.CheckRTMAdjustment($"LoadPriority_SetPower_{requestedTotalPower}"))
                {
                    await GrowattClearAllDeviceNoahTimeSegments();

                    //Only to one device
                    LoggerRTM.LogTrace("Load the power to one device: {requestedTotalPower} W", requestedTotalPower);

                    if (totalPower > 0)
                    {
                        singleDevice.Device.PowerValueRequested = singleDevice.Device.PowerValueCommited + Math.Abs(deltaTotalPower);


                        GrowattEnqueuePowerValue(singleDevice.Device, singleDevice.Device.PowerValueRequested, value.TS);

                        value.PowerValueNewRequested = singleDevice.Device.PowerValueRequested;
                        //Die summe aller anderen devices
                        value.PowerValueNewDeviceSn = singleDevice.Device.DeviceSn;
                    }
                    else if (totalPower < 0)
                    {
                        singleDevice.Device.PowerValueRequested = singleDevice.Device.PowerValueCommited - Math.Abs(deltaTotalPower);
                        GrowattEnqueuePowerValue(singleDevice.Device, singleDevice.Device.PowerValueRequested, value.TS);

                        value.PowerValueNewRequested = singleDevice.Device.PowerValueRequested;
                        //Die summe aller anderen devices
                        value.PowerValueNewDeviceSn = singleDevice.Device.DeviceSn;
                    }
                }
            }
            else
            {

                //Load balance to all devices
                LoggerRTM.LogTrace("Load balance the power to all devices: {absTotalPower} W", deltaTotalPower);

                Queue<IDeviceQuery> deviceQueries = [];

                var remainingPower = Math.Abs(deltaTotalPower);
                foreach (var dc in deviceUtilization)
                {
                    var portion = Math.Min(dc.Capacity, remainingPower);

                    if (totalPower > 0)
                    {
                        dc.Device.PowerValueRequested = dc.Device.PowerValueCommited + portion;
                    }
                    else if (totalPower < 0)
                    {
                        dc.Device.PowerValueRequested = dc.Device.PowerValueCommited - portion;
                    }

                    var item = new DeviceNoahSetPowerQuery()
                    {
                        DeviceType = "noah",
                        DeviceSn = dc.Device.DeviceSn,
                        Value = dc.Device.PowerValueRequested,
                        Force = true,
                        TS = value.TS
                    };

                    deviceQueries.Enqueue(item);

                    dc.Device.PowerValueRequested = dc.Device.PowerValueRequested;
                    dc.Device.PowerValueLastChanged = value.TS;

                    LoggerRTM.LogTrace(messageTemplateRequested, CurrentState.UtcNow, dc.Device.DeviceSn, dc.Device.PowerValueRequested);

                    GrowattDeviceQueryQueueWatchdog.Enqueue(item);

                    ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 204, Key = "TotalPowerRequested", Value = dc.Device.PowerValueRequested.ToString() });
                    value.PowerValueNewRequested = deviceUtilization.Any() ? deviceUtilization.Sum(x => x.Device.PowerValueRequested) : 0;
                    value.PowerValueNewDeviceSn = devices != null
                        ? string.Join(",", devices.Select(s => s.DeviceSn))
                        : string.Empty;

                    remainingPower -= portion;

                    if (remainingPower <= 0)
                        break;

                    _adjustmentWaitCycles = -1;
                }

                var requestedNewTotalPower = requestedTotalPower;

                if (remainingPower > 0 && totalPower > 0)
                {
                    requestedNewTotalPower = requestedTotalPower + remainingPower;
                }
                else if (remainingPower > 0 && totalPower < 0)
                {
                    requestedNewTotalPower = requestedTotalPower - remainingPower;
                }

                if (CurrentState.CheckRTMAdjustment($"LoadPriority_SetPower_{requestedNewTotalPower}"))
                {
                    GrowattDeviceQueryQueueWatchdog.Enqueue(deviceQueries);
                }
            }

            ApiInvokeStateHasChanged();

            await Task.CompletedTask;
        }

        private async Task TibberRTMAdjustment3TotalPowerMax(TibberRealTimeMeasurement value, string deviceSn = "")
        {
            if (CurrentState.CheckRTMAdjustment("LoadPriority_SetPower_840"))
            {
                await GrowattClearAllDeviceNoahTimeSegments();
                await GrowattClearSetPowerAsync(value.TS, 840);

                value.PowerValueNewRequested = 840;
                value.PowerValueNewDeviceSn = deviceSn;
            }
        }

        private async Task TibberRTMAdjustment3TotalPowerMin(TibberRealTimeMeasurement value, string deviceSn = "")
        {
            if (CurrentState.CheckRTMAdjustment("LoadPriority_SetPower_0"))
            {
                await GrowattClearAllDeviceNoahTimeSegments();
                await GrowattClearSetPowerAsync(value.TS, 0);

                value.PowerValueNewRequested = 0;
                value.PowerValueNewDeviceSn = deviceSn;
            }
        }

        private async Task TibberRTMCalculation1(TibberRealTimeMeasurement value)
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                var measurementsQuery = TibberRealTimeMeasurement;

                if (ApiSettingAvgPowerLoadSeconds > 0)
                    measurementsQuery.Where(m => m.TS >= CurrentState.UtcNow.AddSeconds(-ApiSettingAvgPowerLoadSeconds));

                var measurements = measurementsQuery.ToList();

                measurements.Add(value);

                var powerConsumptionCompleteMeasurements = measurements.OrderByDescending(m => m.TS).ToList().GetEnumerator();

                List<TibberRealTimeMeasurement> powerConsumptionUntilZero = [];

                while (powerConsumptionCompleteMeasurements.MoveNext())
                {
                    if (powerConsumptionCompleteMeasurements.Current.Power == 0) break;
                    powerConsumptionUntilZero.Add(powerConsumptionCompleteMeasurements.Current);
                }

                value.PowerAvgConsumption = value.Power > 0 ? (int)powerConsumptionUntilZero.Average(m => m.Power) : 0;

                var powerProductionCompleteMeasurements = measurements.OrderByDescending(m => m.TS).ToList().GetEnumerator();

                List<TibberRealTimeMeasurement> powerProductionUntilZero = [];

                while (powerProductionCompleteMeasurements.MoveNext())
                {
                    if (powerProductionCompleteMeasurements.Current.PowerProduction == 0) break;
                    powerProductionUntilZero.Add(powerProductionCompleteMeasurements.Current);
                }

                value.PowerAvgProduction = value.PowerProduction > 0 ? (int)powerProductionUntilZero.Average(m => m.PowerProduction ?? 0) : 0;
            }

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 101, Key = "PowerAvgConsumption", Value = value.PowerAvgConsumption.ToString() });
            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 102, Key = "PowerAvgProduction", Value = value.PowerAvgProduction.ToString() });

            await Task.CompletedTask;
        }

        private async Task TibberRTMCalculation2(TibberRealTimeMeasurement value)
        {
            lock (TibberRealTimeMeasurement._syncRoot)
            {
                var measurementsQuery = TibberRealTimeMeasurement;

                if (ApiSettingAvgPowerLoadSeconds > 0)
                    measurementsQuery.Where(m => m.TS >= CurrentState.UtcNow.AddSeconds(-ApiSettingAvgPowerLoadSeconds));

                var measurements = measurementsQuery.ToList();

                measurements.Add(value);

                var powerConsumptionCompleteMeasurements = measurements.OrderByDescending(m => m.TS).ToList().GetEnumerator();

                List<TibberRealTimeMeasurement> powerConsumptionUntilZero = [];

                while (powerConsumptionCompleteMeasurements.MoveNext())
                {
                    if (powerConsumptionCompleteMeasurements.Current.Power == 0) break;
                    powerConsumptionUntilZero.Add(powerConsumptionCompleteMeasurements.Current);
                }

                value.PowerAvgConsumption = value.Power > 0 ? (int)powerConsumptionUntilZero.Average(m => m.Power) : 0;

                var powerProductionCompleteMeasurements = measurements.OrderByDescending(m => m.TS).ToList().GetEnumerator();

                List<TibberRealTimeMeasurement> powerProductionUntilZero = [];

                while (powerProductionCompleteMeasurements.MoveNext())
                {
                    if (powerProductionCompleteMeasurements.Current.PowerProduction == 0) break;
                    powerProductionUntilZero.Add(powerProductionCompleteMeasurements.Current);
                }

                value.PowerAvgProduction = value.PowerProduction > 0 ? (int)powerProductionUntilZero.Average(m => m.PowerProduction ?? 0) : 0;
            }

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 101, Key = "PowerAvgConsumption", Value = value.PowerAvgConsumption.ToString() });
            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 201, Key = "PowerAvgProduction", Value = value.PowerAvgProduction.ToString() });

            await Task.CompletedTask;
        }

        private async Task TibberRTMDefaultBatteryPriority(DateTimeOffset? ts = null)
        {
            if (CurrentState.CheckRTMCondition("BatteryPriority_SetPower_0"))
            {
                // If the battery is full and the restriction mode is not expensive activate battery
                // priority and use only surplus power
                await GrowattQueryBattPriorityDeviceNoahTimeSegmentsAsync();
                await GrowattClearSetPowerAsync(ts, 0);
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

                var exists = await dbContext.TibberPrices.AnyAsync(x => x.StartsAt == price.StartsAt);
                // Prüfe, ob der Datensatz bereits existiert
                if (!exists)
                {
                    // Füge den neuen Datensatz hinzu
                    dbContext.TibberPrices.Add(price);
                }
            }
            await dbContext.SaveChangesAsync(); // Änderungen speichern
        }

        #endregion Private Methods

        #region IObservable

        private Timer Timer { get; init; } = null!;
        // Maximal 1 Thread gleichzeitig

        public async void OnNext(RealTimeMeasurement value)
        {
            try
            {
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 1, Key = "GrowattNoahTotalPPV", Value = CurrentState.GrowattNoahTotalPPV.ToString() });
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 2, Key = "WeatherIsCloudy", Value = CurrentState.IsCloudy.ToString() });

                var dbContext = ApiGetDbContext();

                var tibberRealTimeMeasurement = new TibberRealTimeMeasurement(value);

                var calculationElement = dbContext.GrowattElements.FirstOrDefault(x => x.ElementType == GrowattElement.ElementTypes.Calculation && x.IsActive);
                if (calculationElement != null)
                {
                    if (calculationElement.Id == GrowattElements.Calculation1.Id)
                    {
                        await TibberRTMCalculation1(tibberRealTimeMeasurement);
                    }
                    else if (calculationElement.Id == GrowattElements.Calculation2.Id)
                    {
                        await TibberRTMCalculation2(tibberRealTimeMeasurement);
                    }
                }

                if (tibberRealTimeMeasurement == null)
                {
                    Logger.LogWarning("No Tibber real-time measurement available.");
                    return;
                }

                if (!TibberPrices.Any() || (CurrentState.UtcNow.Hour > 13 && CurrentState.UtcNow.AddDays(1).Date != TibberPrices.Max(x => x.StartsAt).Date))
                {
                    if (CurrentState.CheckTibberPricesCondition($"GetTomorrowPrices_{CurrentState.UtcNow.Hour}"))
                    {
                        await TibberGetTomorrowPrices();
                    }
                }

                var firstTime = CurrentState.WeatherForecast?.Hourly?.Time?.FirstOrDefault();

                if (firstTime == null || firstTime != null && DateTime.Parse(firstTime).Date != CurrentState.UtcNow.Date)
                {
                    CurrentState.WeatherForecast = await CurrentState.GetWeatherForecastAsync();
                }

                tibberRealTimeMeasurement.PowerValueTotalDefault = CurrentState.GrowattNoahTotalDefaultPower;
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(tibberRealTimeMeasurement.TS, 3, "RTMTotalPowerDefaultNoah", tibberRealTimeMeasurement.PowerValueTotalDefault.ToString()));

                tibberRealTimeMeasurement.PowerValueTotalCommited = CurrentState.PowerValueTotalCommited;
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(tibberRealTimeMeasurement.TS, 4, "RTMTotalPowerCommited", tibberRealTimeMeasurement.PowerValueTotalCommited.ToString()));

                tibberRealTimeMeasurement.PowerValueTotalRequested = CurrentState.PowerValueTotalRequested;
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(tibberRealTimeMeasurement.TS, 5, "RTMTotalPowerRequested", tibberRealTimeMeasurement.PowerValueTotalRequested.ToString()));

                var ajustmentElement = dbContext.GrowattElements.FirstOrDefault(x => x.ElementType == GrowattElement.ElementTypes.Adjustment && x.IsActive);
                if (ajustmentElement != null)
                {
                    if (ajustmentElement.Id == GrowattElements.Adjustment1.Id)
                    {
                        await TibberRTMAdjustment1(tibberRealTimeMeasurement);
                    }
                    else if (ajustmentElement.Id == GrowattElements.Adjustment2.Id)
                    {
                        await TibberRTMAdjustment2(tibberRealTimeMeasurement);
                    }
                    else if (ajustmentElement.Id == GrowattElements.Adjustment3.Id)
                    {
                        await TibberRTMAdjustment3(tibberRealTimeMeasurement);
                    }
                }

                tibberRealTimeMeasurement.SettingPowerLoadSeconds = ApiSettingAvgPowerLoadSeconds;
                tibberRealTimeMeasurement.SettingOffSetAvg = ApiSettingAvgPowerOffset;
                tibberRealTimeMeasurement.SettingAvgPowerHysteresis = ApiSettingAvgPowerHysteresis;

                lock (TibberRealTimeMeasurement._syncRoot)
                {
                    TibberRealTimeMeasurement.Add(tibberRealTimeMeasurement);
                }

                dbContext.TibberRealTimeMeasurements.Add(tibberRealTimeMeasurement); // Speichern in der Datenbank
                await dbContext.SaveChangesAsync(); // Änderungen speichern

                ApiInvokeStateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
            }
        }

        private async void TimerCallback(object? state)
        {
            try
            {
                await GrowattQueryDevice();
                await GrowattQueryDeviceNoahInfo();
                await GrowattQueryDeviceNoahLastData();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in TimerCallback.");
            }
        }

        #endregion IObservable
    }
}
