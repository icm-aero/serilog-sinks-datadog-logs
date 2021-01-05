using System.Collections.Generic;

namespace NLog.Target.Datadog
{
    public interface IDatadogConfiguration
    {
        string ApiKey { get; set; }
        string Source { get; set; }
        string Service { get; set; }
        string Host { get; set; }
        string[] Tags { get; set; }

        /// <summary>
        /// URL of the server to send log events to.
        /// </summary>
        string Url { get; set; }

        /// <summary>
        /// Port of the server to send log events to.
        /// </summary>
        int Port { get; set; }

        /// <summary>
        /// Use SSL or plain text.
        /// </summary>
        bool UseSSL { get; set; }

        /// <summary>
        /// Use TCP or HTTP.
        /// </summary>
        bool UseTCP { get; set; }

        /// <summary>
        /// Gets or sets whether to include all properties of the log event in the document
        /// </summary>
        bool IncludeAllProperties { get; set; }

        /// <summary>
        /// Gets or sets a comma separated list of excluded properties when setting <see cref="IElasticSearchTarget.IncludeAllProperties"/>
        /// </summary>
        string ExcludedProperties { get; set; }

        /// <summary>
        /// Gets or sets a list of additional fields to add to the elasticsearch document.
        /// </summary>
        IList<Field> Fields { get; set; }

        string ToString();
    }
}