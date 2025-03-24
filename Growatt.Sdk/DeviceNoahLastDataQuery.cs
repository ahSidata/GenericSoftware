using Growatt.Sdk;
using Newtonsoft.Json;

namespace EnergyAutomate.Definitions
{
    public class DeviceNoahLastDataQuery : IDevice
    {
        [JsonIgnore]
        public bool Force { get; set; } = false;

        public string DeviceSn { get; set; } = string.Empty;

        public string DeviceType { get; set; } = string.Empty;

        public string QueryType { get; set; } = string.Empty;
    }
}
