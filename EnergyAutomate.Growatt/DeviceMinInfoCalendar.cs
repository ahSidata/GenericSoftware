namespace EnergyAutomate.Growatt
{
    public class DeviceMinInfoCalendar
    {
        public string? CalendarType { get; set; }
        public int FirstDayOfWeek { get; set; }
        public int MinimalDaysInFirstWeek { get; set; }
        public DeviceMinInfoGregorianChange? GregorianChange { get; set; }
        public TimeZoneInfo? TimeZone { get; set; }
        public int WeekYear { get; set; }
        public DeviceMinInfoTime? Time { get; set; }
        public long TimeInMillis { get; set; }
        public int WeeksInWeekYear { get; set; }
        public bool Lenient { get; set; }
        public bool WeekDateSupported { get; set; }
    }
}