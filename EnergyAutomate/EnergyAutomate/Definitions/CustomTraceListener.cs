using EnergyAutomate.Definitions;
using System.Diagnostics;

public class CustomTraceListener : TraceListener
{
    #region Fields

    public ThreadSafeObservableCollection<CustomTraceLog> LogMessages = new();

    #endregion Fields

    #region Public Methods

    public List<CustomTraceLog> GetLogMessages()
    {
        lock (LogMessages._syncRoot)
        {
            return LogMessages.ToList();
        }
    }

    #endregion Public Methods

    #region Public Methods

    public void AddLog(string? message, string? category = "")
    {
        lock (LogMessages._syncRoot)
        {
            LogMessages.Add(new CustomTraceLog()
            {
                Message = message,
                TS = DateTime.Now,
                Category = category
            });
        }
    }

    public override void Write(string? message)
    {
        lock (LogMessages._syncRoot)
        {
            AddLog(message, "ApiService");
        }
    }

    public override void WriteLine(string? message, string? category)
    {
        lock (LogMessages._syncRoot)
        {
            AddLog(message, category);
        }
    }

    public override void WriteLine(string? message)
    {
        lock (LogMessages._syncRoot)
        {
            AddLog(message, "ApiService");
        }
    }

    #endregion Public Methods
}
