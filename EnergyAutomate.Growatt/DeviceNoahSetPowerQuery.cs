namespace EnergyAutomate.Growatt
{
    public class DeviceNoahSetPowerQuery : IDeviceQuery
    {
        #region Properties

        public string? DeviceSn { get; set; }
        public string? DeviceType { get; set; }
        public bool Force { get; set; } = false;
        public DateTimeOffset? TS { get; set; }
        public int Value { get; set; }

        #endregion Properties

        #region Public Methods

        public FormUrlEncodedContent ToFormUrlEncodedContent()
        {
            var keyValuePairs = new[]
            {
                    new KeyValuePair<string, string?>("deviceSn", DeviceSn),
                    new KeyValuePair<string, string?>("deviceType", DeviceType),
                    new KeyValuePair<string, string?>("value", Value.ToString())
                };

            return new FormUrlEncodedContent(keyValuePairs);
        }

        #endregion Public Methods
    }
}
