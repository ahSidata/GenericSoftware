using Growatt.Sdk;

public class DeviceNoahLastDataResponse : IDeviceDataResponse
{
    #region Properties

    public int Code { get; set; }
    public DeviceNoahLastDataRoot Data { get; set; }
    public string Message { get; set; }

    #endregion Properties
}
