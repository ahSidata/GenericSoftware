using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EnergyAutomate.Growatt
{
    public class DeviceNoahSetLowLimitSocQuery : IDeviceQuery
    {
        #region Properties

        /// <summary>
        /// device SN, example: xxxxxxx
        /// </summary>
        public string DeviceSn { get; set; } = string.Empty;
        /// <summary>
        /// device type, example: noah
        /// </summary>
        public string DeviceType { get; set; } = string.Empty;

        /// <summary>
        /// low limit SOC, example: 0
        /// </summary>
        public int Value { get; set; } = 0;

        [JsonIgnore]
        public bool Force { get; set; } = false;

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
