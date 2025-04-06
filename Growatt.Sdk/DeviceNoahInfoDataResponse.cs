using EnergyAutomate.Growatt;

public class DeviceNoahInfoDataResponse : IDeviceDataResponse
{
    #region Properties

    public int Code { get; set; }
    public DeviceNoahInfoDataRoot Data { get; set; }
    public string Message { get; set; }

    #endregion Properties
}
