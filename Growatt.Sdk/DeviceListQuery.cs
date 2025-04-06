using Newtonsoft.Json;

namespace EnergyAutomate.Growatt
{
    public class DeviceListQuery : IDeviceQuery
    {
        #region Properties

        public string DeviceSn { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;

        [JsonIgnore]
        public bool Force { get; set; } = false;

        public FormUrlEncodedContent ToFormUrlEncodedContent()
        {
            throw new NotImplementedException();
        }

        #endregion Properties
    }
}
