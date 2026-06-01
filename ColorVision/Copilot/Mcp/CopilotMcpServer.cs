using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    public sealed class CopilotMcpServer : IDisposable
    {
        private const int MaxRequestBytes = 1024 * 1024;
        private static readonly ILog Log = LogManager.GetLogger(typeof(CopilotMcpServer));
        private static readonly Lazy<CopilotMcpServer> LazyInstance = new(() => new CopilotMcpServer());

        private readonly object _syncRoot = new();
        private readonly CopilotMcpRequestHandler _requestHandler;
        private CancellationTokenSource? _cts;
        private TcpListener? _listener;
        private Task? _acceptLoopTask;
        private CopilotMcpRuntimeSettings _settings = new();

        private CopilotMcpServer()
        {
            _requestHandler = new CopilotMcpRequestHandler(() => _settings);
        }

        public static CopilotMcpServer Instance => LazyInstance.Value;

        public bool IsRunning { get; private set; }

        public string LastStatusMessage { get; private set; } = "ColorVision MCP server is stopped.";

        public void ApplyConfig()
        {
            var config = CopilotConfig.Instance;
            if (config.EnsureInitialized())
                ColorVision.UI.ConfigHandler.GetInstance().Save<CopilotConfig>();

            ApplySettings(new CopilotMcpRuntimeSettings
            {
                Enabled = config.McpEnabled,
                Host = "127.0.0.1",
                Port = config.McpPort,
                BearerToken = config.McpBearerToken,
            });
        }

        public void ApplySettings(CopilotMcpRuntimeSettings settings)
        {
            lock (_syncRoot)
            {
                var previousPort = _settings.Port;
                _settings = settings ?? new CopilotMcpRuntimeSettings();

                if (!_settings.Enabled)
                {
                    StopNoLock("ColorVision MCP server is disabled.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_settings.BearerToken))
                {
                    StopNoLock("ColorVision MCP server token is missing.");
                    return;
                }

                if (IsRunning && _listener != null && previousPort == _settings.Port)
                {
                    LastStatusMessage = $"ColorVision MCP server is running at {_settings.Endpoint}.";
                    return;
                }

                StartNoLock();
            }
        }

        public void Stop()
        {
            lock (_syncRoot)
            {
                StopNoLock("ColorVision MCP server is stopped.");
            }
        }

        private void StartNoLock()
        {
            StopNoLock("Restarting ColorVision MCP server.");

            try
            {
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, _settings.Port);
                _listener.Start();
                IsRunning = true;
                LastStatusMessage = $"ColorVision MCP server is running at {_settings.Endpoint}.";
                Log.Info(LastStatusMessage);
                _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
            }
            catch (SocketException ex)
            {
                IsRunning = false;
                LastStatusMessage = $"ColorVision MCP server port unavailable at {_settings.Endpoint}: {ex.Message}";
                Log.Error(LastStatusMessage, ex);
                StopNoLock(LastStatusMessage);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                LastStatusMessage = $"ColorVision MCP server failed to start: {ex.Message}";
                Log.Error(LastStatusMessage, ex);
                StopNoLock(LastStatusMessage);
            }
        }

        private void StopNoLock(string statusMessage)
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
            }
            catch
            {
            }
            finally
            {
                _listener = null;
                _cts?.Dispose();
                _cts = null;
                _acceptLoopTask = null;
                IsRunning = false;
                LastStatusMessage = statusMessage;
            }
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient? client = null;
                try
                {
                    if (_listener == null)
                        return;

                    client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    client?.Dispose();
                    return;
                }
                catch (ObjectDisposedException)
                {
                    client?.Dispose();
                    return;
                }
                catch (Exception ex)
                {
                    client?.Dispose();
                    Log.Warn("ColorVision MCP accept loop error.", ex);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using var _ = client;
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(30));
                var stream = client.GetStream();
                var callerSource = client.Client.RemoteEndPoint?.ToString() ?? string.Empty;
                var request = await ReadRequestAsync(stream, callerSource, linkedCts.Token);
                var response = request == null
                    ? new CopilotMcpHttpResponse { StatusCode = 400, Body = "{\"error\":\"Invalid HTTP request.\"}" }
                    : await _requestHandler.HandleAsync(request, linkedCts.Token);
                await WriteResponseAsync(stream, response, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Warn("ColorVision MCP client request failed.", ex);
            }
        }

        private static async Task<CopilotMcpHttpRequest?> ReadRequestAsync(NetworkStream stream, string callerSource, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            using var memory = new MemoryStream();
            var headerEnd = -1;

            while (memory.Length < MaxRequestBytes)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read <= 0)
                    break;

                memory.Write(buffer, 0, read);
                headerEnd = FindHeaderEnd(memory.GetBuffer(), (int)memory.Length);
                if (headerEnd >= 0)
                    break;
            }

            if (headerEnd < 0)
                return null;

            var raw = memory.GetBuffer();
            var headerText = Encoding.ASCII.GetString(raw, 0, headerEnd);
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
                return null;

            var requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestLine.Length < 2)
                return null;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var separator = line.IndexOf(':');
                if (separator <= 0)
                    continue;

                headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }

            var contentLength = 0;
            if (headers.TryGetValue("Content-Length", out var contentLengthText)
                && !int.TryParse(contentLengthText, out contentLength))
            {
                return null;
            }

            if (contentLength > MaxRequestBytes)
                return null;

            var bodyStart = headerEnd + 4;
            var alreadyRead = (int)memory.Length - bodyStart;
            while (alreadyRead < contentLength)
            {
                var remaining = Math.Min(buffer.Length, contentLength - alreadyRead);
                var read = await stream.ReadAsync(buffer.AsMemory(0, remaining), cancellationToken);
                if (read <= 0)
                    break;

                memory.Write(buffer, 0, read);
                alreadyRead += read;
            }

            var data = memory.ToArray();
            var body = contentLength > 0 && data.Length >= bodyStart
                ? Encoding.UTF8.GetString(data, bodyStart, Math.Min(contentLength, data.Length - bodyStart))
                : string.Empty;

            var path = requestLine[1];
            var queryIndex = path.IndexOf('?');
            if (queryIndex >= 0)
                path = path[..queryIndex];

            return new CopilotMcpHttpRequest
            {
                Method = requestLine[0],
                Path = path,
                Headers = headers,
                Body = body,
                CallerSource = callerSource,
            };
        }

        private static async Task WriteResponseAsync(NetworkStream stream, CopilotMcpHttpResponse response, CancellationToken cancellationToken)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(response.Body ?? string.Empty);
            var builder = new StringBuilder();
            builder.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(GetReasonPhrase(response.StatusCode)).Append("\r\n");
            builder.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
            builder.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            builder.Append("Connection: close\r\n");
            foreach (var header in response.Headers)
                builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            builder.Append("\r\n");

            var headerBytes = Encoding.ASCII.GetBytes(builder.ToString());
            await stream.WriteAsync(headerBytes.AsMemory(0, headerBytes.Length), cancellationToken);
            if (bodyBytes.Length > 0)
                await stream.WriteAsync(bodyBytes.AsMemory(0, bodyBytes.Length), cancellationToken);
        }

        private static int FindHeaderEnd(byte[] buffer, int length)
        {
            for (var index = 0; index <= length - 4; index++)
            {
                if (buffer[index] == '\r'
                    && buffer[index + 1] == '\n'
                    && buffer[index + 2] == '\r'
                    && buffer[index + 3] == '\n')
                {
                    return index;
                }
            }

            return -1;
        }

        private static string GetReasonPhrase(int statusCode)
        {
            return statusCode switch
            {
                200 => "OK",
                202 => "Accepted",
                400 => "Bad Request",
                401 => "Unauthorized",
                404 => "Not Found",
                405 => "Method Not Allowed",
                503 => "Service Unavailable",
                _ => "OK",
            };
        }

        public void Dispose()
        {
            Stop();
        }
    }
}