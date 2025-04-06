using Newtonsoft.Json;

namespace EnergyAutomate.Growatt
{
    public class DeviceListData
    {
        #region Properties

        public int Count { get; set; }
        [JsonProperty("data")]
        public List<DeviceList>? Devices { get; set; }
        public int Pages { get; set; }
        public int PageSize { get; set; }

        #endregion Properties
    }
}
