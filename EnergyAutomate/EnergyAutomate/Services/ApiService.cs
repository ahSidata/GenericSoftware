using BlazorBootstrap;
using EnergyAutomate.Definitions;
using EnergyAutomate.Extentions;
using Microsoft.EntityFrameworkCore;
using System.Numerics;

namespace EnergyAutomate.Services
{
    public partial class ApiService
    {
        #region Fields

        private readonly Lock lockAdjustPower = new();
        private readonly Lock lockLoadBalance = new();

        private readonly string messageTemplatePowerSet = "{CurrentState.UtcNow} {Type} ({device}) PowerValue: {powerValue} W";

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
        public int ApiSettingSocMax { get; set; } = 100;
        public int ApiSettingSocMin { get; set; } = 10;
        public int ApiSettingTimeOffset { get; set; } = DateTimeOffset.Now.Offset.Hours;
        public ApiState CurrentState { get; set; }
        public int GrowattDeviceQueryQueueWatchdogCount => GrowattDeviceQueryQueueWatchdog.Count;
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

        public async Task<int> GrowattDeviceQueryQueueWatchdogCountAsync()
        {
            return await GrowattDeviceQueryQueueWatchdog.CountAsync();
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

        public List<DeviceNoahInfoData> GrowattLatestNoahInfoDatas()
        {
            List<DeviceNoahInfoData> result = new List<DeviceNoahInfoData>();
            lock (GrowattDeviceNoahInfoData._syncRoot)
            {
                foreach (var device in GrowattDevices.Where(x => x.DeviceType == "noah"))
                {
                    var infoData = GrowattDeviceNoahInfoData.Where(x => x.DeviceSn == device.DeviceSn).OrderByDescending(x => x.TS).FirstOrDefault();
                    if (infoData != null)
                        result.Add(infoData);
                }
            }
            return result;
        }

        public List<DeviceNoahLastData> GrowattLatestNoahLastDatas()
        {
            List<DeviceNoahLastData> result = new List<DeviceNoahLastData>();
            lock (GrowattDeviceNoahLastData._syncRoot)
            {
                foreach (var device in GrowattDevices.Where(x => x.DeviceType == "noah"))
                {
                    lock (GrowattDeviceNoahLastData._syncRoot)
                    {
                        var lastData = GrowattDeviceNoahLastData.Where(x => x.deviceSn == device.DeviceSn).OrderByDescending(x => x.TS).FirstOrDefault();
                        if (lastData != null)
                            result.Add(lastData);
                    }
                }
            }
            return result;
        }

        public async Task GrowattQueryDevice(bool force = false)
        {
            var item = new DeviceListQuery() { Force = force };

            await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(item);

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

                await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(item);
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

                await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(item);
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

            await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(item);

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

                await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(item);
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
                    await TibberSavePricesAsync(tomorrowList);

                    var todayList = result.Data.Viewer.Home.CurrentSubscription.PriceInfo.Today.Select(x => new TibberPrice()
                    {
                        StartsAt = DateTimeOffset.Parse(x.StartsAt).ToUniversalTime(),
                        Total = x.Total,
                        Level = x.Level
                    }).ToList();
                    await TibberSavePricesAsync(todayList);
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
            return Math.Abs(deviceNoahLastData.totalBatteryPackSoc - deviceNoahLastData.dischargeSocLimit) < 2;
        }

        private static bool GrowattNearofBatterySocFull(DeviceNoahLastData deviceNoahLastData)
        {
            return deviceNoahLastData.totalBatteryPackChargingStatus == 0
                ? Math.Abs(deviceNoahLastData.totalBatteryPackSoc - deviceNoahLastData.chargeSocLimit) < 6
                : false;
        }

        private async Task ApiAutoModeDisabledLoadBalanceRule(TibberRealTimeMeasurement tibberRealTimeMeasurement)
        {
            // If the automatic mode is disabled, the power value is set to 0
            await GrowattClearSetPowerAsync(tibberRealTimeMeasurement.TS);

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

        private async Task ApiAutoModeEnabledLoadBalanceRule(TibberRealTimeMeasurement tibberRealTimeMeasurement)
        {
            // If the automatic mode is enabled and the restriction is not active, the power value
            // is set to 0
            await GrowattClearSetPowerAsync(tibberRealTimeMeasurement.TS);
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

        private async Task GrowattClearAllDeviceNoahTimeSegments()
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattQueryClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue);

            await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(DeviceTimeSegmentQueue);
        }

        private async Task GrowattClearSetPowerAsync(DateTimeOffset ts, int powerValue = 0)
        {
            await GrowattDeviceQueryQueueWatchdog.ClearAsync();

            var devices = GrowattGetDevicesNoahOnline();

            var powerValuePerDevice = powerValue / devices.Count;

            foreach (var device in devices)
            {
                var item = new DeviceNoahSetPowerQuery()
                {
                    DeviceType = "noah",
                    DeviceSn = device.DeviceSn,
                    Value = powerValuePerDevice,
                    Force = true,
                    TS = ts
                };

                await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(item);
                LoggerRTM.LogTrace(messageTemplatePowerSet, "Enqueued", CurrentState.UtcNow, device.DeviceSn, device.PowerValueRequested);


                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 204, Key = "TotalPowerRequested", Value = powerValue.ToString() });
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
                            if (device != null)
                            {
                                LoggerRTM.LogTrace(messageTemplatePowerSet, "Requested", CurrentState.UtcNow, device.DeviceSn, setPowerQuery.Value);

                                lock (GrowattDevices._syncRoot)
                                {
                                    device.PowerValueRequested = setPowerQuery.Value;
                                }

                                await growattApiClient.ExecuteDeviceQueryAsync(item);

                                LoggerRTM.LogTrace(messageTemplatePowerSet, "Commited", CurrentState.UtcNow, device.DeviceSn, setPowerQuery.Value);

                                lock (GrowattDevices._syncRoot)
                                {
                                    device.PowerValueLastChanged = CurrentState.UtcNow;
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

                        GrowattGetNoahInfoDataPerDevice(timeSegmentQuery.DeviceSn)?.SetTimeSegment(timeSegmentQuery);

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

        private string GrowattGetDeviceMinSnList()
        {
            return string.Join(",", GrowattDevices.Where(x => x.DeviceType == "min").Select(x => x.DeviceSn).ToList());
        }

        private string GrowattGetDeviceNoahSnList()
        {
            return string.Join(",", GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList());
        }

        private DeviceNoahSetTimeSegmentQuery GrowattQueryDefaultBattPriorityDeviceNoahTimeSegment(string deviceSn, bool force = true) => new()
        {
            Force = force,
            DeviceSn = deviceSn,
            DeviceType = "noah",
            Type = "1",
            StartTime = "0:0",
            EndTime = "23:59",
            Mode = "1",
            Power = "0",
            Enable = "1"
        };

        private async Task GrowattQueryBattPriorityDeviceNoahTimeSegmentsAsync()
        {
            Queue<IDeviceQuery> DeviceTimeSegmentQueue = new();

            await GrowattQueryClearDeviceNoahTimeSegments(DeviceTimeSegmentQueue, 1);

            var deviceSnList = GrowattDevices.Where(x => x.DeviceType == "noah").Select(x => x.DeviceSn).ToList();

            foreach (var deviceSn in deviceSnList)
            {
                DeviceTimeSegmentQueue.Enqueue(GrowattQueryDefaultBattPriorityDeviceNoahTimeSegment(deviceSn));
            }

            await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(DeviceTimeSegmentQueue);
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
                                StartTime = "00:00",
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

            await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(DeviceTimeSegmentQueue);
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

                lock (GrowattDevices._syncRoot)
                {
                    var device = GrowattDevices.FirstOrDefault(x => x.DeviceSn == deviceNoahInfo.DeviceSn);
                    if (device != null)
                    {
                        device.PowerValueDefault = (int)deviceNoahInfo.DefaultPower;
                    }
                }

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
                        device.IsBatteryEmpty = GrowattNearofBatterySocEmpty(deviceNoahLastData);
                        device.IsBatteryFull = deviceNoahLastData.totalBatteryPackChargingStatus == 0 && GrowattNearofBatterySocFull(deviceNoahLastData);
                        device.Soc = deviceNoahLastData.totalBatteryPackSoc;
                        device.SocMin = deviceNoahLastData.dischargeSocLimit;
                        device.PowerValueSolar = (int)deviceNoahLastData.ppv;
                        device.PowerValueBattery = (int)deviceNoahLastData.totalBatteryPackChargingPower;
                        device.PowerValueOutput = (int)deviceNoahLastData.pac;
                    }
                }

                lock (GrowattDeviceNoahLastData._syncRoot)
                {
                    GrowattDeviceNoahLastData.Add(deviceNoahLastData);
                }

                var deviceDbContext = await dbContext.GrowattDevices.FindAsync(deviceNoahLastData.deviceSn);
                if (deviceDbContext != null)
                {
                    deviceDbContext.IsBatteryEmpty = GrowattNearofBatterySocEmpty(deviceNoahLastData);
                    deviceDbContext.IsBatteryFull = deviceNoahLastData.totalBatteryPackChargingStatus == 0 && GrowattNearofBatterySocFull(deviceNoahLastData);
                    deviceDbContext.Soc = deviceNoahLastData.totalBatteryPackSoc;
                    deviceDbContext.SocMin = deviceNoahLastData.dischargeSocLimit;
                    deviceDbContext.PowerValueSolar = (int)deviceNoahLastData.ppv;
                    deviceDbContext.PowerValueBattery = (int)deviceNoahLastData.totalBatteryPackChargingPower;
                    deviceDbContext.PowerValueOutput = (int)deviceNoahLastData.pac;
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
                    await ApiAutoModeEnabledLoadBalanceRule(value);
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
                            await TibberRTMDefaultBatteryPriorityAsync(value.TS);
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
                            await GrowattDeviceQueryQueueWatchdog.ClearAsync();

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
                        await TibberRTMDefaultBatteryPriorityAsync(value.TS);
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
                        await GrowattDeviceQueryQueueWatchdog.ClearAsync();

                        Logger.LogTrace("AutoMode disabled: Running LoadBalanceRule one time");

                        //If loadbalance is active the battety priority is set
                        await ApiAutoModeDisabledLoadBalanceRule(value);

                        CurrentState.IsRTMAutoModeRunning = false;
                    }
                }
            }
        }

        private async Task TibberRTMAdjustment1PowerSet(TibberRealTimeMeasurement value)
        {
            int calcPowerValue = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);
            int powerValue = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);

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
                var avgPowerConsumption = value.PowerAvgConsumption ?? 0;
                var avgPowerProduction = -value.PowerAvgProduction ?? 0;

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

                powerValue = calcPowerValue > maxPower ? maxPower : calcPowerValue < 0 ? 0 : calcPowerValue;

                if (powerValue <= maxPower && powerValue > 0)
                {
                    if (device.PowerValueRequested != powerValue)
                    {
                        var item = new DeviceNoahSetPowerQuery()
                        {
                            DeviceType = "noah",
                            DeviceSn = device.DeviceSn,
                            Value = powerValue,
                            Force = true,
                            TS = value.TS
                        };

                        await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(item);
                        LoggerRTM.LogTrace(messageTemplatePowerSet, "Enqueued", CurrentState.UtcNow, device.DeviceSn, device.PowerValueRequested);

                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 204, Key = "TotalPowerRequested", Value = powerValue.ToString() });
                    }
                    else
                    {
                        Logger.LogTrace($"PowerValue {device.DeviceSn} already set to {powerValue}");
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

                value.PowerValueNewRequested = powerValue;
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
                        await TibberRTMDefaultLoadPriorityAvgAsync(value.TS);
                    }
                }
            }
            else if (CurrentState.GrowattNoahTotalPPV > 840)
            {
                if (CurrentState.IsGrowattBatteryFull)
                {
                    Logger.LogInformation($"Battery is full, no action needed");

                    await TibberRTMDefaultBatteryPriorityAsync(value.TS);
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
                        await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
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
                        await TibberRTMDefaultBatteryPriorityAsync(value.TS);
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
                            await TibberRTMDefaultBatteryPriorityAsync(value.TS);
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
                            await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
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
            int powerValue = Math.Abs(value.TotalPower - ApiSettingAvgPowerOffset);

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

                powerValue = calcPowerValue > maxPower ? maxPower : calcPowerValue < 0 ? 0 : calcPowerValue;

                if (powerValue <= maxPower && powerValue > 0)
                {
                    if (device.PowerValueRequested != powerValue)
                    {
                        var item = new DeviceNoahSetPowerQuery()
                        {
                            DeviceType = "noah",
                            DeviceSn = device.DeviceSn,
                            Value = powerValue,
                            Force = true,
                            TS = value.TS
                        };

                        await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(item);
                        LoggerRTM.LogTrace(messageTemplatePowerSet, "Enqueued", CurrentState.UtcNow, device.DeviceSn, device.PowerValueRequested);

                        ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 204, Key = "TotalPowerRequested", Value = powerValue.ToString() });
                    }
                    else
                    {
                        Logger.LogTrace($"PowerValue {device.DeviceSn} already set to {powerValue}");
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
            if(ApiSettingAutoMode)
            {
                await TibberRTMAdjustment3AutoMode(value);
                return;
            }

            var last5Minutes = CurrentState.GrowattNoahGetAvgPpvLast5Minutes();

            if (last5Minutes < ApiSettingAvgPower + 100)
            {
                if (CurrentState.IsGrowattBatteryEmpty)
                {
                    await TibberRTMDefaultLoadPriorityAvgAsync(value.TS);

                    LoggerRTM.LogInformation($"Battery is empty, set power to 0");
                }
                else if (CurrentState.IsGrowattBatteryFull)
                {
                    // If the battery is not full and the restriction mode is cheap load with
                    // full soloar power
                    await TibberRTMDefaultLoadPriorityAvgAsync(value.TS);
                }
                else
                {
                    if (CurrentState.IsExpensiveRestrictionMode)
                    {
                        await TibberRTMAdjustment3AutoMode(value);
                    }
                    else if (CurrentState.IsCheapRestrictionMode)
                    {
                        await TibberRTMDefaultBatteryPriorityAsync(value.TS);
                    }
                    else
                    {
                        if (CurrentState.IsCloudy && !CurrentState.IsBelowAvgPrice)
                        {
                            await TibberRTMDefaultLoadPriorityAvgAsync(value.TS);
                        }
                        else
                        {
                            await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
                        }
                    }
                }
            }
            else if (last5Minutes > 840)
            {
                if (CurrentState.IsGrowattBatteryFull)
                {
                    LoggerRTM.LogInformation($"Battery is full, no action needed");

                    await TibberRTMDefaultBatteryPriorityAsync(value.TS);
                }
                else
                {
                    //If cloudy
                    if (CurrentState.IsCloudy)
                    {
                        await TibberRTMAdjustment3AutoMode(value);
                        ApiSettingAvgPowerHysteresis = 10;
                        ApiSettingAvgPowerOffset = -25;
                    }
                    else
                    {
                        if (CurrentState.IsCheapRestrictionMode)
                        {
                            // If the battery is not full and the restriction mode is cheap load
                            // with full soloar power
                            await TibberRTMDefaultBatteryPriorityAsync(value.TS);
                        }
                        else
                        {
                            await TibberRTMDefaultLoadPriorityMaxAsync(value, GrowattGetDeviceNoahSnList());
                        }
                    }
                }
            }
            else
            {
                if (CurrentState.IsGrowattBatteryFull)
                {
                    if (CurrentState.IsExpensiveRestrictionMode)
                    {
                        await TibberRTMAdjustment3AutoMode(value);
                    }
                    else
                    {
                        await TibberRTMDefaultBatteryPriorityAsync(value.TS);
                    }
                }
                else
                {
                    //If cloudy
                    if (CurrentState.IsCloudy)
                    {
                        if (CurrentState.IsExpensiveRestrictionMode)
                        {
                            await TibberRTMAdjustment3AutoMode(value);
                        }
                        else
                        {
                            await TibberRTMDefaultBatteryPriorityAsync(value.TS);
                        }
                    }
                    else
                    {
                        if (CurrentState.IsExpensiveRestrictionMode || !CurrentState.IsBelowAvgPrice)
                        {
                            //Battery is not full and it's not not cloudy and expensive restriction mode so we force load
                            await TibberRTMAdjustment3AutoMode(value);
                        }
                        else
                        {
                            // If the tibber price is not expensive and below avg price we not use
                            // the barttery
                            await TibberRTMDefaultLoadPrioritySolarInputAsync(value);
                        }
                    }
                }
            }
        }
        private async Task TibberRTMAdjustment3AutoMode(TibberRealTimeMeasurement value)
        {
            await TibberRTMCheckConditionAsync("LoadPriority_SetPower", async () =>
            {
                await GrowattClearAllDeviceNoahTimeSegments();
            }, () =>
            {
                // Check if any time segments are enabled
                var anyEnabledTimesegments = GrowattLatestNoahInfoDatas().Any(x => x!.TimeSegments.Any(x => x.Enable == "1"));

                return Task.FromResult(!anyEnabledTimesegments);
            });

            await TibberRTMAdjustment3SetPower(value);
        }

        private async Task TibberRTMAdjustment3SetPower(TibberRealTimeMeasurement value)
        {
            LoggerRTM.LogTrace("Starting TibberRTMAdjustment3SetPower. TotalPower: {TotalPower}, ApiSettingAvgPower: {TargetPower}",
                value.TotalPower, ApiSettingAvgPower);

            if (_adjustmentWaitCycles < ApiSettingPowerAdjustmentWaitCycles)
            {
                _adjustmentWaitCycles++;
                LoggerRTM.LogTrace("Wait because WaitCycles: {WaitCycles} < {PowerAdjustmentWaitCycles}",
                    _adjustmentWaitCycles, ApiSettingPowerAdjustmentWaitCycles);
                return;
            }

            _adjustmentWaitCycles = 0;
            LoggerRTM.LogTrace("Reset adjustment wait cycles to 0.");

            var powerValueTotalCommited = CurrentState.PowerValueTotalCommited;
            int deltaTotalPower = value.TotalPower - ApiSettingAvgPower;
            var upperlimit = ApiSettingAvgPower + (ApiSettingAvgPowerHysteresis / 2);
            var lowerlimit = ApiSettingAvgPower - (ApiSettingAvgPowerHysteresis / 2);

            LoggerRTM.LogTrace("Calculated limits. DeltaTotalPower: {DeltaTotalPower}, UpperLimit: {UpperLimit}, LowerLimit: {LowerLimit}, CommitedTotalPower: {CommitedTotalPower}",
                deltaTotalPower, upperlimit, lowerlimit, powerValueTotalCommited);

            var onlineDevices = GrowattGetDevicesNoahOnline();
            if (onlineDevices == null || !onlineDevices.Any())
            {
                LoggerRTM.LogTrace("No devices available for adjustment.");
                return;
            }

            // Bezug: Über oberem Limit → Hochregeln
            if (value.TotalPower > upperlimit)
            {
                var requestedTotalPower = ApiSettingMaxPower;
                LoggerRTM.LogTrace("Increasing power to maximum. RequestedTotalPower: {RequestedTotalPower}", requestedTotalPower);

                int maxPowerPerDevice = requestedTotalPower / onlineDevices.Count;

                var devices = onlineDevices.OrderByDescending(o => o.Soc).ToList();
                foreach (var device in devices)
                {
                    LoggerRTM.LogTrace("Device {DeviceSn} - SoC: {Soc}%, PowerValueCommited: {PowerValue}, IsBatteryEmpty: {IsBatteryEmpty}, IsBatteryFull: {IsBatteryFull}",
                        device.DeviceSn, device.Soc, device.PowerValueCommited, device.IsBatteryEmpty, device.IsBatteryFull);

                    if (device.IsBatteryEmpty)
                    {
                        LoggerRTM.LogTrace("Skipping device {DeviceSn} - battery too low (SoC: {Soc}%).",
                            device.DeviceSn, device.Soc);
                        continue;
                    }

                    int newPower;
                    if (device.PowerValueCommited <= 0)
                    {
                        newPower = Math.Min(Math.Max(100, device.Soc * 4), maxPowerPerDevice);
                        LoggerRTM.LogTrace("REAKTIVIERE Gerät {DeviceSn} mit SoC {Soc}% auf: {NewPower}W",
                            device.DeviceSn, device.Soc, newPower);
                    }
                    else
                    {
                        newPower = Math.Min(Math.Max(device.PowerValueCommited + 100, (int)(device.PowerValueCommited * 1.5)), maxPowerPerDevice);
                        LoggerRTM.LogTrace("Device {DeviceSn} - Setting to maximum available power: {NewPower}W",
                            device.DeviceSn, newPower);
                    }

                    device.PowerValueRequested = newPower;
                    await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                    {
                        DeviceType = "noah",
                        DeviceSn = device.DeviceSn,
                        Value = newPower,
                        Force = true,
                        TS = value.TS
                    });
                }
            }
            // Einspeisung: Unter unterem Limit → Schnell reduzieren
            else if (value.TotalPower < lowerlimit)
            {
                int baseReductionPercent = 60;
                if (value.TotalPower < -100)
                {
                    baseReductionPercent = 80;
                }

                int maxReduction = powerValueTotalCommited * baseReductionPercent / 100;

                if (value.TotalPower < -150 && powerValueTotalCommited > 300)
                {
                    maxReduction = powerValueTotalCommited - ApiSettingAvgPower;
                }

                int reactionFactor = (value.TotalPower < -100) ? 3 : 2;
                int desiredReduction = Math.Min(Math.Abs(value.TotalPower) * reactionFactor, maxReduction);

                int minimumTotalPower = ApiSettingAvgPower;
                var requestedTotalPower = Math.Max(powerValueTotalCommited - desiredReduction, minimumTotalPower);

                if (value.TotalPower < -200)
                {
                    minimumTotalPower = ApiSettingAvgPower / 2;
                    requestedTotalPower = Math.Max(minimumTotalPower, powerValueTotalCommited - desiredReduction);
                }

                LoggerRTM.LogTrace("SCHNELLERE Reduktion wegen Einspeisung. RequestedTotalPower: {RequestedTotalPower} (max reduction: {MaxReduction}W, {BaseReductionPercent}%)",
                    requestedTotalPower, maxReduction, baseReductionPercent);

                if (requestedTotalPower < powerValueTotalCommited)
                {
                    int remainingReduction = powerValueTotalCommited - requestedTotalPower;
                    var activeDevices = onlineDevices.Where(d => d.PowerValueCommited > 0).ToList();
                    if (!activeDevices.Any())
                    {
                        LoggerRTM.LogTrace("No active devices to reduce power.");
                        return;
                    }

                    var devices = activeDevices.OrderBy(o => o.Soc).ToList();
                    double totalCommitedActivePower = activeDevices.Sum(d => d.PowerValueCommited);
                    double remainingReductionDouble = remainingReduction;

                    foreach (var device in devices)
                    {
                        if (remainingReduction <= 0 || device.PowerValueCommited <= 0)
                            continue;

                        double powerShare = device.PowerValueCommited / totalCommitedActivePower;
                        int deviceReduction = (int)(remainingReductionDouble * powerShare);

                        int maxDeviceReduction = Math.Min(device.PowerValueCommited,
                                                       Math.Max(device.PowerValueCommited * baseReductionPercent / 100, 50));
                        int actualReduction = Math.Min(deviceReduction, maxDeviceReduction);

                        if (device.Soc < 20)
                        {
                            actualReduction = Math.Min(device.PowerValueCommited, remainingReduction);
                        }

                        int newPower = Math.Max(0, device.PowerValueCommited - actualReduction);

                        LoggerRTM.LogTrace("Device {DeviceSn} reducing power by {Reduction}W. Setting to: {NewPower}W",
                            device.DeviceSn, actualReduction, newPower);

                        device.PowerValueRequested = newPower;
                        await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                        {
                            DeviceType = "noah",
                            DeviceSn = device.DeviceSn,
                            Value = newPower,
                            Force = true,
                            TS = value.TS
                        });

                        remainingReduction -= actualReduction;
                    }
                }
            }
            // Im Zielbereich: keine Leistungsregelung, optionales Loadbalancing
            else
            {
                LoggerRTM.LogTrace("No adjustment needed. TotalPower within target range.");

                // --- Optionales SoC-basiertes Loadbalancing bei größerer Differenz (z.B. >5%) ---
                var devices = onlineDevices.ToList();
                var maxSocDev = devices.OrderByDescending(d => d.Soc).First();
                var minSocDev = devices.OrderBy(d => d.Soc).First();

                if (maxSocDev.Soc - minSocDev.Soc >= 5)
                {
                    int shift = 10; // z.B. 10 W umschichten

                    int maxShiftUp = (ApiSettingMaxPower / devices.Count) - maxSocDev.PowerValueCommited;
                    int maxShiftDown = minSocDev.PowerValueCommited; // nur bis 0W

                    int actualShift = Math.Min(shift, Math.Min(maxShiftUp, maxShiftDown));

                    if (actualShift > 0)
                    {
                        LoggerRTM.LogTrace("Loadbalancing: Umschichten von {Shift}W von Device {MinDev} (SoC {MinSoc}%) zu Device {MaxDev} (SoC {MaxSoc}%)",
                            actualShift, minSocDev.DeviceSn, minSocDev.Soc, maxSocDev.DeviceSn, maxSocDev.Soc);

                        minSocDev.PowerValueRequested = minSocDev.PowerValueCommited - actualShift;
                        maxSocDev.PowerValueRequested = maxSocDev.PowerValueCommited + actualShift;

                        await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                        {
                            DeviceType = "noah",
                            DeviceSn = minSocDev.DeviceSn,
                            Value = minSocDev.PowerValueRequested,
                            Force = true,
                            TS = value.TS
                        });

                        await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(new DeviceNoahSetPowerQuery
                        {
                            DeviceType = "noah",
                            DeviceSn = maxSocDev.DeviceSn,
                            Value = maxSocDev.PowerValueRequested,
                            Force = true,
                            TS = value.TS
                        });
                    }
                    else
                    {
                        LoggerRTM.LogTrace("Loadbalancing: Kein Spielraum zum Umschichten.");
                    }
                }
                else
                {
                    LoggerRTM.LogTrace("Loadbalancing: SoC-Differenz <5%, keine Aktion.");
                }
            }

            LoggerRTM.LogTrace("Final adjustments completed.");

            await Task.CompletedTask;
        }

        // Hilfsfunktion für aktuelle Einspeisung
        private int GetSumOfDeviceCommitedPower()
        {
            var onlineDevices = GrowattGetDevicesNoahOnline();
            if (onlineDevices == null) return 0;
            return onlineDevices.Sum(d => d.PowerValueCommited);
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

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 101, Key = "PowerAvgConsumption", Value = value.PowerAvgConsumption.Value.ToString() });
            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 102, Key = "PowerAvgProduction", Value = value.PowerAvgProduction.Value.ToString() });

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

            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 101, Key = "PowerAvgConsumption", Value = value.PowerAvgConsumption.Value.ToString() });
            ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 201, Key = "PowerAvgProduction", Value = value.PowerAvgProduction.Value.ToString() });

            await Task.CompletedTask;
        }

        private async Task TibberRTMCheckAdjustmentAsync(string condition, Func<Task> callback)
        {
            if (CurrentState.ActiveRTMAdjustment != condition)
            {
                CurrentState.ActiveRTMAdjustment = condition;
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 51, Key = "ActiveRTMAdjustment", Value = condition });
                Logger.LogTrace("CheckRTMAdjustment {condition}", condition);

                await callback.Invoke();
            }
        }

        private async Task TibberRTMCheckConditionAsync(string condition, Func<Task> callback, Func<Task<bool>>? validation = null)
        {
            if (CurrentState.ActiveRTMCondition != condition)
            {
                CurrentState.ActiveRTMCondition = condition;
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 51, Key = "ActiveRTMCondition", Value = condition });
                Logger.LogTrace("CheckRTMCondition {condition}", condition);

                await callback.Invoke();
            }
            else
            {
                if (validation != null)
                {
                    var result = await validation.Invoke();
                    if (result)
                    {
                        LoggerRTM.LogTrace("CheckRTMCondition {condition} validation success", condition);
                    }
                    else
                    {
                        LoggerRTM.LogTrace("CheckRTMCondition {condition} validation failed, set values again !", condition);
                        await callback.Invoke();
                    }
                }
            }
        }

        private async Task TibberRTMDefaultBatteryPriorityAsync(DateTimeOffset ts)
        {
            await TibberRTMCheckConditionAsync("BatteryPriority_SetPower_0",
                async () =>
                {
                    // If the battery is full and the restriction mode is not expensive activate battery
                    // priority and use only surplus power
                    await GrowattQueryBattPriorityDeviceNoahTimeSegmentsAsync();
                    await GrowattClearSetPowerAsync(ts, 0);
                }, async () =>
                {
                    // Check if any time segments are enabled
                    var anyTimesegmentEnabled = GrowattLatestNoahInfoDatas()
                        .All(n => n.TimeSegments.Any(t => t.Equals(GrowattQueryDefaultBattPriorityDeviceNoahTimeSegment(n.DeviceSn, false))));
                    var allOtherTimesegmentsDisabled = GrowattLatestNoahInfoDatas()
                        .All(n => n.TimeSegments.Where(t => !t.Equals(GrowattQueryDefaultBattPriorityDeviceNoahTimeSegment(n.DeviceSn, false))).All(y => y.Enable == "0"));

                    // Check if power default and commited values are equal to avg
                    var allDevicesConform = GrowattGetDevicesNoahOnline().All(x => x.PowerValueCommited == 0);

                    return await Task.FromResult(anyTimesegmentEnabled && allOtherTimesegmentsDisabled && allDevicesConform);
                });
        }

        private async Task TibberRTMDefaultLoadPriorityAvgAsync(DateTimeOffset ts)
        {
            // If the battery is not empty and the restriction mode is not expensive activate avg injection
            await TibberRTMCheckConditionAsync($"LoadPriority_SetPower_Avg_{ApiSettingAvgPower}",
                async () =>
                {
                    LoggerRTM.LogInformation($"No solar power, set power to AVG power output value");
                    await GrowattClearAllDeviceNoahTimeSegments();
                    await GrowattClearSetPowerAsync(ts, ApiSettingAvgPower);
                }, async() =>
                {
                    // Check if any time segments are enabled
                    var allTimesegmentsDisabled = GrowattLatestNoahInfoDatas().All(x => x!.TimeSegments.All(x => x.Enable == "0"));


                    var avgPerDevice = ApiSettingAvgPower / GrowattGetDevicesNoahOnline().Count;

                    // Check if power default and commited values are equal to avg
                    var allDevicesConform = GrowattGetDevicesNoahOnline().All(x => x.PowerValueCommited == avgPerDevice);

                    return await Task.FromResult(allTimesegmentsDisabled && allDevicesConform);
                }
            );
        }

        private async Task TibberRTMDefaultLoadPriorityMaxAsync(TibberRealTimeMeasurement value, string deviceSn = "")
        {
            await TibberRTMCheckConditionAsync($"LoadPriority_SetPower_{ApiSettingMaxPower}", async () =>
            {
                await GrowattClearAllDeviceNoahTimeSegments();
                await GrowattClearSetPowerAsync(value.TS, ApiSettingMaxPower);

                value.PowerValueNewRequested = ApiSettingMaxPower;
                value.PowerValueNewDeviceSn = deviceSn;
            }, async () =>
            {
                // Check if any time segments are enabled
                var anyEnabledTimesegments = GrowattLatestNoahInfoDatas().Any(x => x!.TimeSegments.Any(x => x.Enable == "1"));

                // Check if power default and commited values are equal to avg
                var allDevicesConform = GrowattGetDevicesNoahOnline().All(x => x.PowerValueCommited == ApiSettingMaxPower);

                return await Task.FromResult(!anyEnabledTimesegments && allDevicesConform);
            });
        }

        private async Task TibberRTMDefaultLoadPrioritySolarInputAsync(TibberRealTimeMeasurement value, int reductionPerMinute = 0)
        {
            await TibberRTMCheckConditionAsync("BatteryPriority_SetPower_SolarInput", async () =>
            {
                await GrowattClearAllDeviceNoahTimeSegments();
                await GrowattClearSetPowerAsync(value.TS, 0);
            });

            Queue<IDeviceQuery> queue = [];

            var devices = GrowattDevices.Where(x => x.DeviceType == "noah").ToList();

            var totalPPV = 0;
            foreach (var device in devices)
            {
                var infoData = GrowattGetNoahInfoDataPerDevice(device.DeviceSn);
                var lastData = GrowattGetNoahLastDataPerDevice(device.DeviceSn);
                var powerValue = (int)(lastData?.ppv - reductionPerMinute ?? 0);


                    totalPPV += powerValue;
                    var item = new DeviceNoahSetPowerQuery()
                    {
                        DeviceType = "noah",
                        DeviceSn = device.DeviceSn,
                        Value = powerValue,
                        Force = true,
                        TS = value.TS
                    };

                queue.Enqueue(item);
            }

            await TibberRTMCheckAdjustmentAsync($"LoadPriority_SolarInput_{totalPPV}", async () =>
            {
                await GrowattDeviceQueryQueueWatchdog.EnqueueAsync(queue);              
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue() { Index = 204, Key = "TotalPowerRequested", Value = totalPPV.ToString() });

                await Task.CompletedTask;
            });

        }

        private async Task TibberSavePricesAsync(IList<TibberPrice> prices)
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

                if (!TibberPrices.Where(x => x.StartsAt.Date == CurrentState.UtcNow.Date).Any() || (CurrentState.UtcNow.Hour > 13 && CurrentState.UtcNow.AddDays(1).Date != TibberPrices.Max(x => x.StartsAt).Date))
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

                tibberRealTimeMeasurement.PowerValueTotalDefault = CurrentState.GrowattNoahTotalDefaultPower;
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(tibberRealTimeMeasurement.TS, 3, "RTMTotalPowerDefaultNoah", (tibberRealTimeMeasurement.PowerValueTotalDefault ?? 0).ToString()));

                tibberRealTimeMeasurement.PowerValueTotalCommited = CurrentState.PowerValueTotalCommited;
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(tibberRealTimeMeasurement.TS, 4, "RTMTotalPowerCommited", (tibberRealTimeMeasurement.PowerValueTotalCommited ?? 0).ToString()));

                tibberRealTimeMeasurement.PowerValueTotalRequested = CurrentState.PowerValueTotalRequested;
                ApiSettingAvgPowerAdjustmentTraceValues.AddOrUpdate(new APiTraceValue(tibberRealTimeMeasurement.TS, 5, "RTMTotalPowerRequested", (tibberRealTimeMeasurement.PowerValueTotalRequested ?? 0).ToString()));

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
