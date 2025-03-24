namespace Growatt.Sdk
{
    public interface IDevice
    {
        #region Properties

        public string DeviceSn { get; set; }

        public string DeviceType { get; set; }

        bool Force { get; set; }

        #endregion Properties
    }
}
