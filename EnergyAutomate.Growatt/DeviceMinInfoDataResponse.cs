using EnergyAutomate.Growatt;

public class DeviceMinInfoDataResponse : IDeviceDataResponse
{
    #region Properties

    public int Code { get; set; }
    public DeviceMinInfoDataRoot Data { get; set; }
    public string Message { get; set; }

    #endregion Properties
}
