using EnergyAutomate.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

public class CustomLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new ConcurrentDictionary<string, ILogger>();
    private Func<string, bool>? _categoryFilter;

    public CustomLoggerProvider(IServiceProvider serviceProvider, LogLevel logLevel, Func<string, bool>? categoryFilter)
    {
        ServiceProvider = serviceProvider;
        _categoryFilter = categoryFilter;
        LogLevel = logLevel;
    }

    public ConcurrentBag<CustomTraceLog> LogMessages { get; set; } = [];

    public void LogMessagesAdd(CustomTraceLog item)
    {
        LogMessages.Add(item);
        LogMessagesChanged?.Invoke();
    }

    public event Action? LogMessagesChanged;

    public LogLevel LogLevel { get; set; } = LogLevel.Trace;

    public IServiceProvider? ServiceProvider { get; set; }

    public ILoggerFactory? LoggerFactory => ServiceProvider?.GetRequiredService<ILoggerFactory>();

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new CustomLogger(name, this, _categoryFilter));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}
