using Serilog.Events;

namespace Serilog.Sinks.Datadog.Logs
{
    public delegate ILogFormatter LogFormatterFactory(string source, string service, string host, string[] tags);

    public interface ILogFormatter
    {
        /// <summary>
        /// formatMessage enrich the log event with DataDog metadata such as source, service, host and tags.
        /// </summary>
        string formatMessage(LogEvent logEvent);
    }
}