using Newtonsoft.Json;

namespace Growatt.OSS
{
    public class DeviceListData
    {
        #region Properties

        public int Count { get; set; }
        [JsonProperty("data")]
        public List<Device>? Devices { get; set; }
        public int Pages { get; set; }
        public int PageSize { get; set; }

        #endregion Properties
    }
}
