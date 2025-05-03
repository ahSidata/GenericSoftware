namespace EnergyAutomate.Growatt
{
    public interface IDeviceQuery
    {
        #region Properties

        public string? DeviceSn { get; set; }

        public string? DeviceType { get; set; }

        bool Force { get; set; }

        FormUrlEncodedContent ToFormUrlEncodedContent();

        #endregion Properties
    }
}
