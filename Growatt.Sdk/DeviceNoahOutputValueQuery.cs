using Growatt.Sdk;
using Newtonsoft.Json;

namespace EnergyAutomate.Definitions
{
    public class DeviceNoahOutputValueQuery : IDevice
    {
        [JsonIgnore]
        public bool Force { get; set; } = false;

        public DateTime TS { get; set; }

        public string DeviceSn { get; set; } = string.Empty;

        public string DeviceType { get; set; } = string.Empty;

        public int Value { get; set; }
    }
}
