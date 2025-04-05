namespace Growatt.Sdk
{
    public class DeviceNoahHistoricalDataResponse : IDeviceDataResponse
    {
        #region Properties

        public int Code { get; set; }
        public DeviceNoahHistoricalDataRoot Data { get; set; }
        public string Message { get; set; }

        #endregion Properties
    }
}
