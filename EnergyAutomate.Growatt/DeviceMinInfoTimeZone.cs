namespace EnergyAutomate.Growatt
{
    public class DeviceMinInfoTimeZone
    {
        public bool Dirty { get; set; }
        public object LastRuleInstance { get; set; }
        public int DSTSavings { get; set; }
        public string DisplayName { get; set; }
        public int RawOffset { get; set; }
        public string ID { get; set; }
    }
}