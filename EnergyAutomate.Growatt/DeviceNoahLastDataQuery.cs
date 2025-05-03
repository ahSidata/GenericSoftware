using Newtonsoft.Json;

namespace EnergyAutomate.Growatt
{
    public class DeviceNoahLastDataQuery : IDeviceQuery
    {
        #region Properties

        public string? DeviceSn { get; set; }
        public string? DeviceType { get; set; }
        [JsonIgnore]
        public bool Force { get; set; } = false;

        #endregion Properties

        #region Public Methods

        public FormUrlEncodedContent ToFormUrlEncodedContent()
        {
            var keyValuePairs = new[]
            {
                new KeyValuePair<string, string?>("deviceSn", DeviceSn),
                new KeyValuePair<string, string?>("deviceType", DeviceType),
            };

            return new FormUrlEncodedContent(keyValuePairs);
        }

        #endregion Public Methods
    }
}
