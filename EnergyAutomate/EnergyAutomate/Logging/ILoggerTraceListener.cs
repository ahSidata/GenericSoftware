using EnergyAutomate.Logging;
using System.Diagnostics;

public class ILoggerTraceListener : TraceListener
{
    private readonly ILoggerProvider _loggerProvider;

    public ILoggerTraceListener(IServiceProvider serviceProvider)
    {
        _loggerProvider = serviceProvider.GetRequiredService<ILoggerProvider>();
    }

    private CustomLoggerProvider LoggerProvider => (CustomLoggerProvider)_loggerProvider;

    public override void WriteLine(string? message, string? category)
    {
        LoggerProvider.LogMessagesAdd(new CustomTraceLog
        {
            Category = category,
            LogLevel = LogLevel.Trace,
            EventId = new EventId(0, category),
            Message = message,
            Exception = null,
            TS = DateTime.Now
        });
    }

    public override void Write(string? message)
    {
        // Optional: Buffering, siehe unten
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message) && !message.StartsWith("EnergyAutomate."))
        {
            LoggerProvider.LogMessagesAdd(new CustomTraceLog
            {
                Category = "ApiService",
                LogLevel = LogLevel.Trace,
                EventId = new EventId(5, "Trace"),
                Message = message,
                Exception = null,
                TS = DateTime.Now
            });
        }
    }
}
