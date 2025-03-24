namespace Growatt.OSS
{
    public class Device
    {
        #region Properties

        public DateTime CreateDate { get; set; }
        public string DeviceSn { get; set; }
        public string DeviceType { get; set; }
        public DateTime? IsOfflineSince { get; set; }

        #endregion Properties
    }
}
