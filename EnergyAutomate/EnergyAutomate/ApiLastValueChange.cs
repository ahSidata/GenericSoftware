using System.Text.Json.Serialization;

namespace EnergyAutomate
{
    public class ApiOutputValueDeviceInfo
    {
        public bool Force = false;

        public DateTime TS { get; set; }

        public string DeviceSn { get; set; } = string.Empty;

        public string DeviceType { get; set; } = string.Empty;

        public int Value { get; set; }
    }
}
