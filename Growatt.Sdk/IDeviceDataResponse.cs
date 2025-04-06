namespace EnergyAutomate.Growatt
{
    public interface IDeviceDataResponse
    {
        int Code { get; set; }
        string Message { get; set; }
    }
}