using EnergyAutomate;
using EnergyAutomate.Definitions;
using EnergyAutomate.Watchdogs;
using Growatt.OSS;
using Growatt.Sdk;

public class ApiServiceInfo
{
    #region Public Constructors

    /// <summary></summary>
    /// <param name="serviceProvider"></param>
    public ApiServiceInfo(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    #endregion Public Constructors

    #region Properties

    public int AvgPowerLoad { get; set; }
    public int DeltaPowerValue { get; set; }

    public int NewPowerValue { get; set; }
    public bool SettingAutoMode { get; set; }
    public bool SettingAutoModeRestriction { get; set; } = false;
    public int SettingDataReadsDelay { get; set; } = 6000;
    public bool SettingLoadBalanced { get; set; } = false;
    public int SettingLockSeconds { get; set; } = 600;
    public int SettingMaxPower { get; set; } = 840;
    public int SettingOffsetAvg { get; set; } = 50;
    public int SettingPowerLoadSeconds { get; set; } = 12;

    #region AdjustPower

    public int LastLoggedPowerValue { get; set; }
    public int SettingAvgPowerAdjustmentStep { get; set; } = 10;
    public int SettingAvgPowerHysteresis { get; set; } = 25;
    public DateTime SettingAvgPowerlastAdjustmentTime { get; set; }
    public int SettingAvgPowerLastDifference { get; set; }

    #endregion AdjustPower

    private IServiceProvider ServiceProvider { get; init; }

    #endregion Properties

    #region Growatt

    public event EventHandler? StateHasChanged;

    public ApiQueueWatchdog<DeviceNoahLastDataQuery> DeviceNoahLastDataQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahLastDataQuery>>();

    public ApiQueueWatchdog<DeviceNoahOutputValueQuery> DeviceNoahOutputValueQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahOutputValueQuery>>();

    public ApiQueueWatchdog<DeviceNoahTimeSegmentQuery> DeviceNoahTimeSegmentQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahTimeSegmentQuery>>();

    public bool DataReadsDoRefresh(DeviceNoahLastDataQuery.QueryTypes queryType)
    {
        return DataReads.Any(x => x.MethodeName == DeviceNoahLastDataQuery.QueryTypes.DeviceNoahInfo && x.TimeStamp > DateTime.Now.AddSeconds(-SettingDataReadsDelay));
    }

    public int GetNoahDeviceCount()
    {
        return Devices.Where(x => x.DeviceType == "noah").Count();
    }

    public int GetNoahCurrentPowerValueSum()
    {
        return Devices
            .Where(w => w.DeviceType == "noah")
            .Sum(noah => (int)(GetNoahLastDataPerDevice(noah.DeviceSn)?.pac ?? 0));
    }

    public int GetNoahCurrentIsDischarchingState()
    {
        return Devices
            .Where(w => w.DeviceType == "noah")
            .Max(noah => GetNoahLastDataPerDevice(noah.DeviceSn)?.totalBatteryPackChargingStatus ?? 0);
    }

    public DeviceNoahInfo? GetNoahInfoPerDevice(string deviceSn)
    {
        return DeviceNoahInfo.FirstOrDefault(x => x.DeviceSn == deviceSn);
    }

    public DeviceNoahLastData? GetNoahLastDataPerDevice(string deviceSn)
    {
        var result = DeviceNoahLastData.Where(x => x.deviceSn == deviceSn).OrderByDescending(x => x.time).FirstOrDefault();
        return result;
    }

    public void InvokeStateHasChanged()
    {
        StateHasChanged?.Invoke(this, new EventArgs());
    }

    #region Properties

    public List<ApiCallLog> DataReads { get; set; } = [];
    public ThreadSafeObservableCollection<DeviceNoahInfo> DeviceNoahInfo { get; set; } = [];
    public ThreadSafeObservableCollection<DeviceNoahLastData> DeviceNoahLastData { get; set; } = [];
    public ThreadSafeObservableCollection<Device> Devices { get; set; } = [];

    #endregion Properties

    #region Events

    public delegate Task ClearDeviceNoahTimeSegmentsHandler(object sender, EventArgs e);

    public delegate Task RefreshDeviceListHandler(object sender, EventArgs e);

    public delegate Task RefreshNoahLastDataHandler(object sender, EventArgs e);

    public delegate Task RefreshNoahsHandler(object sender, EventArgs e);

    public event ClearDeviceNoahTimeSegmentsHandler? ClearDeviceNoahTimeSegments;

    public event RefreshDeviceListHandler? RefreshDeviceList;

    public event RefreshNoahLastDataHandler? RefreshNoahLastData;

    public event RefreshNoahsHandler? RefreshNoahs;

    public async Task InvokeClearDeviceNoahTimeSegments()
    {
        if (ClearDeviceNoahTimeSegments != null)
        {
            await ClearDeviceNoahTimeSegments(this, EventArgs.Empty);
        }
    }

    public async Task InvokeRefreshDeviceList()
    {
        if (RefreshDeviceList != null)
        {
            await RefreshDeviceList(this, EventArgs.Empty);
        }
    }

    public async Task InvokeRefreshNoahs()
    {
        if (RefreshNoahs != null)
        {
            await RefreshNoahs(this, EventArgs.Empty);
        }
    }

    public async Task InvokeRefreshNoahsLastData()
    {
        if (RefreshNoahLastData != null)
        {
            await RefreshNoahLastData(this, EventArgs.Empty);
        }
    }

    #endregion Events

    #endregion Growatt

    #region Tibber

    public ThreadSafeObservableCollection<ApiPrice> Prices { get; set; } = [];

    public ThreadSafeObservableCollection<RealTimeMeasurementExtention> RealTimeMeasurement { get; set; } = [];

    public bool GetCurrentAutoModeRestriction()
    {
        return Prices.FirstOrDefault(x => x.StartsAt.Date == DateTime.Now.Date)?.AutoModeRestriction ?? false;
    }

    public int? GetLastCommitedPowerValue() => GetLastCommitedPowerValueItem()?.CommitedPowerValue;

    public RealTimeMeasurementExtention? GetLastCommitedPowerValueItem()
    {
        lock (RealTimeMeasurement._syncRoot)
        {
            return RealTimeMeasurement.Where(x => x.Timestamp > DateTime.Now.AddMinutes(-1) && x.RequestedPowerValue != null && x.CommitedPowerValue != null).OrderByDescending(x => x.Timestamp).FirstOrDefault();
        }
    }

    public int? GetLastRequestedPowerValue() => GetLastRequestedPowerValueItem()?.RequestedPowerValue;

    public RealTimeMeasurementExtention? GetLastRequestedPowerValueItem()
    {
        lock (RealTimeMeasurement._syncRoot)
        {
            return RealTimeMeasurement.Where(x => x.Timestamp > DateTime.Now.AddMinutes(-1) && x.RequestedPowerValue != null && x.CommitedPowerValue == null).OrderByDescending(x => x.Timestamp).FirstOrDefault();
        }
    }

    public List<double?> GetPriceTodayDatas()
    {
        var priceDates = Prices.GroupBy(x => x.StartsAt.Date).ToList();
        var result = priceDates.OrderByDescending(x => x.Key).Take(2);
        var today = result.OrderBy(x => x.Key).FirstOrDefault()?.Key;
        var dataToday = today.HasValue ? Prices.Where(x => x.StartsAt.Date == today.Value.Date).OrderBy(x => x.StartsAt).Select(x => (double?)x.Total).ToList() : new List<double?>();

        return dataToday;
    }

    public List<double?> GetPriceTomorrowDatas()
    {
        var priceDates = Prices.GroupBy(x => x.StartsAt.Date).ToList();
        var result = priceDates.OrderByDescending(x => x.Key).Take(2);
        var tomorrow = result.OrderBy(x => x.Key).LastOrDefault()?.Key;
        var dataTomorrow = tomorrow.HasValue ? Prices.Where(x => x.StartsAt.Date == tomorrow.Value.Date).OrderBy(x => x.StartsAt).Select(x => (double?)x.Total).ToList() : new List<double?>();

        return dataTomorrow;
    }

    #endregion Tibber
}
