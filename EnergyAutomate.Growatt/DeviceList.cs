namespace EnergyAutomate.Growatt
{
    public class DeviceList
    {
        #region Properties

        public DateTime CreateDate { get; set; }
        public string? DeviceSn { get; set; }
        public string? DeviceType { get; set; }
        public DateTimeOffset? IsOfflineSince { get; set; }
        public int PowerValueCommited { get; set; }
        public DateTimeOffset? PowerValueLastChanged { get; set; }
        public int PowerValueRequested { get; set; }

        public int PowerValueDefault { get; set; }

        public int PowerValueOutput { get; set; }

        public int PowerValueSolar { get; set; }
        public int PowerValueBatteryPower { get; set; }
        public int PowerValueBatteryStatus { get; set; }

        public bool IsBatteryEmpty { get; set; }
        public bool IsBatteryFull { get; set; }

        public int Soc { get; set; }

        public int SocMin { get; set; }

        #endregion Properties
    }
}
