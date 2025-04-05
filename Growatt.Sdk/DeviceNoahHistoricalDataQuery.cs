using Newtonsoft.Json;

namespace Growatt.Sdk
{
    public class DeviceNoahHistoricalDataQuery : IDeviceQuery
    {
        #region Properties

        public enum QueryTypes
        {
            DeviceNoahInfo,
            DeviceNoahLastData,
            DeviceNoahTimeSegment,
            SetPowerAsync
        }
        public string Date { get; set; } = string.Empty;
        public string DeviceSn { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
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
                new KeyValuePair<string, string>("date", Date)
            };

            return new FormUrlEncodedContent(keyValuePairs);
        }

        #endregion Public Methods
    }
}
