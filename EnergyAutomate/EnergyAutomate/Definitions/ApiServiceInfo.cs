using EnergyAutomate;
using EnergyAutomate.Definitions;
using EnergyAutomate.Watchdogs;
using Growatt.OSS;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

public class ApiServiceInfo
{
    private IServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceProvider"></param>
    public ApiServiceInfo(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public bool SettingAutoMode { get; set; }

    public bool SettingAutoModeRestriction { get; set; } = false;

    public bool SettingLoadBalanced { get; set; } = false;

    public int SettingPowerLoadSeconds { get; set; } = 30;

    public int SettingLockSeconds { get; set; } = 600;

    public int SettingOffsetAvg { get; set; } = 75;

    public int SettingToleranceAvg { get; set; } = 50;

    public int SettingMaxPower { get; set; } = 840;

    public int AvgPowerLoad { get; set; }

    public int DeltaPowerValue { get; set; }

    public int DifferencePowerValue { get; set; }

    public int? LastRequestedPowerValue => RealTimeMeasurement.Where(x => x.RequestedPowerValue != null).OrderByDescending(x => x.Timestamp).FirstOrDefault()?.RequestedPowerValue;

    public int? LastCommitedPowerValue => RealTimeMeasurement.Where(x => x.RequestedPowerValue != null).OrderByDescending(x => x.Timestamp).FirstOrDefault()?.CommitedPowerValue;

    public int NewPowerValue { get; set; }

    public List<ApiCallLog> DataReads { get; set; } = [];

    #region Growatt

    #region OutputValue

    public event EventHandler? StateHasChanged;

    public void InvokeStateHasChanged()
    {
        StateHasChanged?.Invoke(this, new EventArgs());
    }

    public ApiQueueWatchdog<DeviceNoahOutputValueQuery> DeviceNoahOutputValueQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahOutputValueQuery>>();

    public ApiQueueWatchdog<DeviceNoahTimeSegmentQuery> DeviceNoahTimeSegmentQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahTimeSegmentQuery>>();

    public ApiQueueWatchdog<DeviceNoahLastDataQuery> DeviceNoahLastDataQueueWatchdog => ServiceProvider.GetRequiredService<ApiQueueWatchdog<DeviceNoahLastDataQuery>>();

    
    public DeviceNoahLastData? GetNoahLastDataPerDevice(string deviceSn)
    {
        return DeviceNoahLastData.Where(x => x.deviceSn == deviceSn).OrderByDescending(x => x.time).FirstOrDefault();
    }

    public DeviceNoahInfo? GetNoahInfoPerDevice(string deviceSn)
    {
        return DeviceNoahInfo.FirstOrDefault(x => x.DeviceSn == deviceSn);
    }

    #endregion OutputValue

    #region Device

    public ThreadSafeObservableCollection<Device> Devices { get; set; } = [];

    public ThreadSafeObservableCollection<DeviceNoahInfo> DeviceNoahInfo { get; set; } = [];

    public ThreadSafeObservableCollection<DeviceNoahLastData> DeviceNoahLastData { get; set; } = [];

    #endregion Device

    #region Events

    public delegate Task RefreshDeviceListHandler(object sender, EventArgs e);
    public event RefreshDeviceListHandler? RefreshDeviceList;

    public delegate Task ClearDeviceNoahTimeSegmentsHandler(object sender, EventArgs e);
    public event ClearDeviceNoahTimeSegmentsHandler? ClearDeviceNoahTimeSegments;

    public delegate Task RefreshNoahsHandler(object sender, EventArgs e);
    public event RefreshNoahsHandler? RefreshNoahs;

    public delegate Task RefreshNoahLastDataHandler(object sender, EventArgs e);
    public event RefreshNoahLastDataHandler? RefreshNoahLastData;

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

    public ThreadSafeObservableCollection<RealTimeMeasurementExtention> RealTimeMeasurement { get; set; } = [];

    public ThreadSafeObservableCollection<ApiPrice> Prices { get; set; } = [];

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

    public bool GetCurrentAutoModeRestriction()
    {
        return Prices.FirstOrDefault(x => x.StartsAt.Date == DateTime.Now.Date)?.AutoModeRestriction ?? false;
    }

    #endregion Tibber
}
