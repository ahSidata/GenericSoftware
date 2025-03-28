using Growatt.Sdk;

namespace EnergyAutomate.Definitions
{
    public class DeviceNoahSetPowerQuery : IDeviceQuery
    {
        #region Properties

        public string DeviceSn { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public bool Force { get; set; } = false;
        public DateTime? TS { get; set; }
        public int Value { get; set; }

        #endregion Properties

        #region Public Methods

        public FormUrlEncodedContent ToFormUrlEncodedContent()
        {
            var keyValuePairs = new[]
            {
                new KeyValuePair<string, string>("deviceSn", DeviceSn),
                new KeyValuePair<string, string>("deviceType", DeviceType),
                new KeyValuePair<string, string>("value", Value.ToString())
            };

            return new FormUrlEncodedContent(keyValuePairs);
        }

        #endregion Public Methods
    }
}
