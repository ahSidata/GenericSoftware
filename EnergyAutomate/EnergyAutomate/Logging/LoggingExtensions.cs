public static class LoggingExtensions
{
    public static ILoggingBuilder AddCustomLogger(this ILoggingBuilder builder, LogLevel logLevel = LogLevel.Information, Func<string, bool> categoryFilter = null)
    {
        builder.Services.AddSingleton<ILoggerProvider>(sp => new CustomLoggerProvider(sp, logLevel , categoryFilter));
        return builder;
    }
}
