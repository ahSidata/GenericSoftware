namespace Growatt.Sdk
{
    public interface IDeviceDataResponse
    {
        int Code { get; set; }
        string Message { get; set; }
    }
}