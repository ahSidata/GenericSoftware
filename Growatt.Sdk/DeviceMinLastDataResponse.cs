using Growatt.Sdk;

public class DeviceMinLastDataResponse : IDeviceDataResponse
{
    #region Properties

    public int Code { get; set; }
    public DeviceMinLastDataRoot Data { get; set; }
    public string Message { get; set; }

    #endregion Properties
}
