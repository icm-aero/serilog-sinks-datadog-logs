// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0.
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2019 Datadog, Inc.

using System.Collections.Generic;

namespace NLog.Target.Datadog
{
    public interface IDatadogClient
    {
        /// <summary>
        /// Send payload to Datadog logs-backend.
        /// </summary>
        /// <param name="events">Serilog events to send.</param>
        void WriteAsync(IEnumerable<string> events);

        /// <summary>
        /// Cleanup existing resources.
        /// </summary>
        void Close();
    }
}
