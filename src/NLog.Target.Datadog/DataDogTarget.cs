using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog.Common;
using NLog.Config;
using NLog.Targets;

namespace NLog.Target.Datadog
{
    [Target("DataDog")]
    public class DataDogTarget: TargetWithLayout, IDatadogConfiguration //TargetWithContext,
    {
        /// <summary>
        /// The Datadog logs-backend URL.
        /// </summary>
        public const string DDUrl = "https://http-intake.logs.datadoghq.com";

        /// <summary>
        /// The Datadog logs-backend TCP SSL port.
        /// </summary>
        public const int DDPort = 10516;

        /// <summary>
        /// The Datadog logs-backend TCP unsecure port.
        /// </summary>
        public const int DDPortNoSSL = 10514;

        public string ApiKey { get; set; }
        public string Source { get; set; }
        public string Service { get; set; }
        public string Host { get; set; }
        public string[] Tags { get; set; }

        /// <summary>
        /// URL of the server to send log events to.
        /// </summary>
        public string Url { get; set; } = DDUrl;

        /// <summary>
        /// Port of the server to send log events to.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Use SSL or plain text.
        /// </summary>
        public bool UseSSL { get; set; }

        /// <summary>
        /// Use TCP or HTTP.
        /// </summary>
        public bool UseTCP { get; set; }

        /// <summary>
        /// Gets or sets whether to include all properties of the log event in the document
        /// </summary>
        public bool IncludeAllProperties { get; set; }

        /// <summary>
        /// Gets or sets a comma separated list of excluded properties when setting <see cref="IElasticSearchTarget.IncludeAllProperties"/>
        /// </summary>
        public string ExcludedProperties { get; set; }

        /// <summary>
        /// Gets or sets a list of additional fields to add to the elasticsearch document.
        /// </summary>
        [ArrayParameter(typeof(Field), "field")]
        public IList<Field> Fields { get; set; } = new List<Field>();

        private IDatadogClient _client;

        private HashSet<string> _excludedProperties = new HashSet<string>(new[] { "CallerMemberName", "CallerFilePath", "CallerLineNumber", "MachineName", "ThreadId" });
        private JsonSerializer _jsonSerializer;
        private readonly Lazy<JsonSerializerSettings> _jsonSerializerSettings = new Lazy<JsonSerializerSettings>(CreateJsonSerializerSettings, LazyThreadSafetyMode.PublicationOnly);

        private JsonSerializer JsonSerializer => _jsonSerializer ?? (_jsonSerializer = JsonSerializer.CreateDefault(_jsonSerializerSettings.Value));
        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        public DataDogTarget()
        {
            Name = "DataDog";
        }

        protected override void InitializeTarget()
        {
            if (Port == 0)
            {
                Port = UseSSL ? DDPort : DDPortNoSSL;
            }

            base.InitializeTarget();

            if (UseTCP)
            {
                _client = new DatadogTcpClient(Url, Port, UseSSL, ApiKey);
            }
            else
            {
                _client = new DatadogHttpClient(Url, ApiKey);
            }

        }

        protected override void Write(LogEventInfo logEvent)
        {

        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            EmitBatchAsync(new[] { logEvent });
        }

        protected override void Write(IList<AsyncLogEventInfo> logEvents)
        {
            EmitBatchAsync(logEvents);
        }

        protected void EmitBatchAsync(IList<AsyncLogEventInfo> events)
        {
            try
            {
                if (!events.Any())
                {
                    return;
                }

                var formattedEvents = events.Select(FormPayload);
                _client.WriteAsync(formattedEvents);

                foreach (var ev in events)
                {
                    ev.Continuation(null);
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex.FlattenToActualException(), "ElasticSearch: Error while sending log messages");
                foreach (var ev in events)
                {
                    ev.Continuation(ex);
                }
            }
        }

        public string FormPayload(AsyncLogEventInfo ev)
        {
            var logEvent = ev.LogEvent;

            //var document = GetAllProperties(logEvent);
            //document.Add("date", logEvent.TimeStamp);
            //document.Add("level", logEvent.Level.Name);
            //document.Add("message", RenderLogEvent(Layout, logEvent));

            var document = new Dictionary<string, object>
                {
                    {"date", logEvent.TimeStamp},
                    {"level", logEvent.Level.Name},
                    {"message", RenderLogEvent(Layout, logEvent)}
                };

            if (Source != null) { document.Add("ddsource", Source); }
            if (Service != null) { document.Add("service", Service); }
            if (Host != null) { document.Add("host", Host); }
            if (Tags != null) { document.Add("ddtags", Tags); }

            if (logEvent.Exception != null)
            {
                var jsonString = JsonConvert.SerializeObject(logEvent.Exception, _jsonSerializerSettings.Value);
                var ex = JsonConvert.DeserializeObject<ExpandoObject>(jsonString);
                document.Add("exception", ex.ReplaceDotInKeys());
            }

            foreach (var field in Fields)
            {
                var renderedField = RenderLogEvent(field.Layout, logEvent);

                if (string.IsNullOrWhiteSpace(renderedField))
                    continue;

                try
                {
                    document[field.Name] = renderedField.ToSystemType(field.LayoutType, logEvent.FormatProvider, JsonSerializer);
                }
                catch (Exception ex)
                {
                    _jsonSerializer = null; // Reset as it might now be in bad state
                    InternalLogger.Error(ex, "ElasticSearch: Error while formatting field: {0}", field.Name);
                }
            }

            if (IncludeAllProperties && logEvent.HasProperties)
            {
                foreach (var p in logEvent.Properties)
                {
                    var propertyKey = p.Key.ToString();
                    if (_excludedProperties.Contains(propertyKey))
                        continue;
                    if (document.ContainsKey(propertyKey))
                        continue;

                    document[propertyKey] = p.Value;
                }
            }

            var result = JsonConvert.SerializeObject(document, Formatting.None, settings);
            return result;
        }

        private static JsonSerializerSettings CreateJsonSerializerSettings()
        {
            var jsonSerializerSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, CheckAdditionalContent = true };
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            return jsonSerializerSettings;
        }

        protected override void Dispose(bool disposing)
        {
            _client.Close();
            base.Dispose(disposing);
        }
    }
}
