namespace Growatt.Sdk
{
    public class DeviceListResponse
    {
        #region Properties

        public int Code { get; set; }
        public DeviceListData Data { get; set; }
        public string Message { get; set; }

        #endregion Properties
    }
}
