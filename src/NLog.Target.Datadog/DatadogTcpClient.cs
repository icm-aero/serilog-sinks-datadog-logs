// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0.
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2019 Datadog, Inc.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NLog.Common;

namespace NLog.Target.Datadog
{
    /// <summary>
    /// TCP Client that forwards log events to Datadog.
    /// </summary>
    public class DatadogTcpClient : IDatadogClient
    {
        private readonly string _url;
        private readonly int _port;
        private readonly bool _useSsl;
        private readonly string _apiKey;
        private TcpClient _client;
        private Stream _stream;

        /// <summary>
        /// API Key / message-content delimiter.
        /// </summary>
        private const string WhiteSpace = " ";

        /// <summary>
        /// Message delimiter.
        /// </summary>
        private const string MessageDelimiter = "\n";

        /// <summary>
        /// Max number of retries when sending failed.
        /// </summary>
        private const int MaxRetries = 5;

        /// <summary>
        /// Max backoff used when sending failed.
        /// </summary>
        private const int MaxBackoff = 30;

        /// <summary>
        /// Shared UTF8 encoder.
        /// </summary>
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding();

        public DatadogTcpClient(string url, int port, bool useSSL, string apiKey)
        {
            _url = url;
            _port = port;
            _useSsl = useSSL;
            _apiKey = apiKey;
            InternalLogger.Info("Creating TCP client with config: URL: {0}, Port: {1}, UseSSL: {2}", url, port, useSSL);
        }

        /// <summary>
        /// Initialize a connection to Datadog logs-backend.
        /// </summary>
        private async Task ConnectAsync()
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_url, _port);
            Stream rawStream = _client.GetStream();
            if (_useSsl)
            {
                SslStream secureStream = new SslStream(rawStream);
                await secureStream.AuthenticateAsClientAsync(_url);
                _stream = secureStream;
            }
            else
            {
                _stream = rawStream;
            }
        }

        public void WriteAsync(IEnumerable<string> events)
        {
            Task.WhenAll(WriteAsyncImplementation(events)).GetAwaiter().GetResult();
        }

        private async Task WriteAsyncImplementation(IEnumerable<string> events)
        {
            var payloadBuilder = new StringBuilder();
            foreach (var logEvent in events)
            {
                payloadBuilder.Append(_apiKey + WhiteSpace);
                payloadBuilder.Append(logEvent);
                payloadBuilder.Append(MessageDelimiter);
            }
            string payload = payloadBuilder.ToString();

            for (int retry = 0; retry < MaxRetries; retry++)
            {
                int backoff = (int)Math.Min(Math.Pow(retry, 2), MaxBackoff);
                if (retry > 0)
                {
                    await Task.Delay(backoff * 1000);
                }

                if (IsConnectionClosed())
                {
                    try
                    {
                        await ConnectAsync();
                    }
                    catch (Exception e)
                    {
                        InternalLogger.Error(e, "Could not connect to Datadog");
                        continue;
                    }
                }

                try
                {
                    InternalLogger.Trace("Sending payload to Datadog: {0}", payload);
                    byte[] data = UTF8.GetBytes(payload);
                    _stream.Write(data, 0, data.Length);
                    return;
                }
                catch (Exception e)
                {
                    CloseConnection();
                    InternalLogger.Error(e, "Could not send data to Datadog");
                }
            }
            InternalLogger.Error("Could not send payload to Datadog: {0}", payload);
        }

        private void CloseConnection()
        {
#if NETSTANDARD1_3
            _client.Dispose();
            _stream.Dispose();
#else
            _client.Close();
            _stream.Close();
#endif
            _stream = null;
            _client = null;
        }

        private bool IsConnectionClosed()
        {
            return _client == null || _stream == null;
        }

        /// <summary>
        /// Close the client.
        /// </summary>
        public void Close()
        {
            if (!IsConnectionClosed())
            {
                try
                {
                    _stream.Flush();
                }
                catch (Exception e)
                {
                    InternalLogger.Error(e, "Could not flush the remaining data");
                }
                CloseConnection();
            }
        }
    }
}
