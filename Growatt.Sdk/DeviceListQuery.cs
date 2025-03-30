using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Growatt.Sdk
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
