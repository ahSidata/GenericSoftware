using EnergyAutomate;
using Growatt.OSS;
using System.Collections.ObjectModel;
using Tibber.Sdk;

public class ApiServiceInfo
{
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

    public int LastPowerValue { get; set; }

    public int NewPowerValue { get; set; }
  
    public List<ApiCallLog> DataReads { get; set; } = [];

    #region Growatt

    #region OutputValueValueChange

    public event EventHandler? StateHasChanged;

    public void InvokeStateHasChanged()
    {
        StateHasChanged?.Invoke(this, new EventArgs());
    }

    public Queue<ApiOutputValueDeviceInfo> GrowattSetPowerQueue = new();


    public List<ApiOutputValueDeviceInfo> LastOutputValues => RealTimeMeasurement.OrderByDescending(x => x.Timestamp).FirstOrDefault()?.DeviceInfos ?? new List<ApiOutputValueDeviceInfo>();

    public int LastOutputValueSum => LastOutputValues?.Sum(s => s.Value) ?? 0;

    public int GetLastValuePerDevice(string deviceSn)
    {
        if (!string.IsNullOrWhiteSpace(deviceSn))
        {
            var apiOutputValueDeviceInfo = LastOutputValues?.FirstOrDefault(x => x.DeviceSn == deviceSn);
            var lastValueChange = apiOutputValueDeviceInfo?.Value ?? 0;
            return lastValueChange;
        }
        return 0;
    }    
    
    public DeviceNoahLastData? GetNoahLastDataPerDevice(string deviceSn)
    {
        return DeviceNoahLastData.Where(x => x.deviceSn == deviceSn).OrderByDescending(x => x.time).FirstOrDefault();
    }

    public DeviceNoahInfo? GetNoahInfoPerDevice(string deviceSn)
    {
        return DeviceNoahInfo.FirstOrDefault(x => x.DeviceSn == deviceSn);
    }

    #endregion OutputValueValueChange

    #region Device

    public ObservableCollection<Device> Devices { get; set; } = [];
    
    public ObservableCollection<DeviceNoahInfo> DeviceNoahInfo { get; set; } = [];
    
    public ObservableCollection<DeviceNoahLastData> DeviceNoahLastData { get; set; } = [];

    #endregion Device

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


    #endregion Growatt

    #region Tibber

    public ObservableCollection<RealTimeMeasurementExtention> RealTimeMeasurement { get; set; } = [];
    
    public ObservableCollection<ApiPrice> Prices { get; set; } = [];

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

    public int GetStartHoure()
    {
        var dataToday = GetPriceTodayDatas();
        var avg = dataToday.OrderByDescending(price => price).Take(10).Average();
        var firstValueGreaterThanAvg = Prices
            .Where(x => (double?)x.Total > avg)
            .OrderBy(x => x.StartsAt)
            .FirstOrDefault();

        return firstValueGreaterThanAvg != null ? firstValueGreaterThanAvg.StartsAt.Hour : 0;
    }

    public double? GetCurrentPrice()
    {
        //current Price is over the average price
        var dataToday = Prices.Where(x => x.StartsAt.Date == DateTime.Now.Date).OrderBy(x => x.StartsAt).Select(x => new { x.StartsAt, x.Total }).ToList();
        return (double?)dataToday.First(p => p.StartsAt.Hour == DateTime.Now.Hour).Total;
    }

    #endregion Tibber
}
