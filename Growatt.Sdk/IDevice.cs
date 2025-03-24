using System;
using System.Collections.Generic;
using System.Text;

namespace Growatt.Sdk
{
    public interface IDevice
    {
        public string DeviceSn { get; set; }

        public string DeviceType { get; set; }

        bool Force { get; set; }
    }
}
