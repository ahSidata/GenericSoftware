namespace Growatt.Sdk
{
    public class DeviceMinInfoDataRoot
    {
        public string ErrorMsg { get; set; }
        public Dictionary<string, DeviceMinInfoData> Data { get; set; }
        public int ErrorCode { get; set; }
    }
}