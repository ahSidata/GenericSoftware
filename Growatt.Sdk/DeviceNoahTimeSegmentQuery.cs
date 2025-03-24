using Growatt.Sdk;
using Newtonsoft.Json;

namespace Growatt.OSS
{
    public class DeviceNoahTimeSegmentQuery : IDevice
    {
        #region Properties

        /// <summary>device SN, example: xxxxxxx</summary>
        public string DeviceSn { get; set; }
        /// <summary>device type, example: noah</summary>
        public string DeviceType { get; set; }
        /// <summary>time period switch (0: off, 1: on), example: 0</summary>
        public string Enable { get; set; }
        /// <summary>end time (hours:minutes), Example: 02:05</summary>
        public string EndTime { get; set; }
        [JsonIgnore]
        public bool Force { get; set; } = false;
        /// <summary>machine mode (0: load priority, 1: battery priority), example: 0</summary>
        public string Mode { get; set; }
        /// <summary>output power (range 0-800 in w), example: 150</summary>
        public string Power { get; set; }
        /// <summary>start time (hours:minutes), Example: 01:06</summary>
        public string StartTime { get; set; }
        /// <summary>time period (1~9), example: 1</summary>
        public string Type { get; set; }

        #endregion Properties

        #region Public Methods

        public FormUrlEncodedContent ToFormUrlEncodedContent()
        {
            var keyValuePairs = new[]
            {
            new KeyValuePair<string, string>("deviceSn", DeviceSn),
            new KeyValuePair<string, string>("deviceType", DeviceType),
            new KeyValuePair<string, string>("type", Type),
            new KeyValuePair<string, string>("startTime", StartTime),
            new KeyValuePair<string, string>("endTime", EndTime),
            new KeyValuePair<string, string>("mode", Mode),
            new KeyValuePair<string, string>("power", Power),
            new KeyValuePair<string, string>("enable", Enable)
        };

            return new FormUrlEncodedContent(keyValuePairs);
        }

        #endregion Public Methods
    }
}
