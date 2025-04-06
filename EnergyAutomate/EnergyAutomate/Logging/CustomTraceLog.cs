namespace EnergyAutomate.Logging
{
    public class CustomTraceLog
    {
        #region Properties

        public string? Category { get; set; }

        public LogLevel LogLevel { get; set; }

        public EventId EventId { get; set; }

        public string? Message { get; set; }

        public string? Exception { get; set; }

        public DateTime TS { get; set; }

        #endregion Properties
    }
}
