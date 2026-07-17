#pragma warning disable CA1001,CA1861 // The client gate has the same process-wide lifetime as the singleton server.
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    public sealed class CopilotMcpServer : IDisposable
    {
        public const int MaximumConcurrentClients = 16;
        private const int MaxRequestHeaderBytes = 64 * 1024;
        private const int MaxRequestBodyBytes = 1024 * 1024;
        private static readonly ILog Log = LogManager.GetLogger(typeof(CopilotMcpServer));
        private static readonly Lazy<CopilotMcpServer> LazyInstance = new(() => new CopilotMcpServer());

        private readonly object _syncRoot = new();
        private readonly CopilotMcpRequestHandler _requestHandler;
        private readonly SemaphoreSlim _clientSlots = new(MaximumConcurrentClients, MaximumConcurrentClients);
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

        public int ActiveClientCount => MaximumConcurrentClients - _clientSlots.CurrentCount;

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
                LastStatusMessage = $"ColorVision MCP server port unavailable at {_settings.Endpoint}: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}";
                Log.Error(LastStatusMessage, ex);
                StopNoLock(LastStatusMessage);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                LastStatusMessage = $"ColorVision MCP server failed to start: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}";
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
                    if (!_clientSlots.Wait(0, CancellationToken.None))
                    {
                        client.Dispose();
                        client = null;
                        continue;
                    }

                    // Always enter the handler so its using scope disposes the accepted socket.
                    // Passing an already-cancelled token to Task.Run can skip the delegate entirely.
                    var acceptedClient = client;
                    client = null;
                    _ = Task.Run(() => HandleClientWithLeaseAsync(acceptedClient, cancellationToken), CancellationToken.None);
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

        private async Task HandleClientWithLeaseAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                await HandleClientAsync(client, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _clientSlots.Release();
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

            while (memory.Length <= MaxRequestHeaderBytes)
            {
                var remainingHeaderBytes = MaxRequestHeaderBytes - (int)memory.Length;
                var readLength = Math.Min(buffer.Length, Math.Max(1, remainingHeaderBytes + 1));
                var read = await stream.ReadAsync(buffer.AsMemory(0, readLength), cancellationToken);
                if (read <= 0)
                    break;

                memory.Write(buffer, 0, read);
                headerEnd = FindHeaderEnd(memory.GetBuffer(), (int)memory.Length);
                if (headerEnd >= 0)
                {
                    if (headerEnd + 4 > MaxRequestHeaderBytes)
                        return null;
                    break;
                }

                if (memory.Length > MaxRequestHeaderBytes)
                    return null;
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
                && (!int.TryParse(contentLengthText, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength)
                    || contentLength < 0))
            {
                return null;
            }

            if (contentLength > MaxRequestBodyBytes)
                return null;

            var bodyStart = headerEnd + 4;
            var alreadyRead = (int)memory.Length - bodyStart;
            var isChunked = headers.TryGetValue("Transfer-Encoding", out var transferEncoding)
                && transferEncoding.Split(',').Any(value => string.Equals(value.Trim(), "chunked", StringComparison.OrdinalIgnoreCase));
            if (isChunked && headers.ContainsKey("Content-Length"))
                return null;

            string body;
            if (isChunked)
            {
                body = await ReadChunkedBodyAsync(stream, memory.GetBuffer().AsMemory(bodyStart, Math.Max(0, alreadyRead)), cancellationToken);
            }
            else
            {
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
                body = contentLength > 0 && data.Length >= bodyStart
                    ? Encoding.UTF8.GetString(data, bodyStart, Math.Min(contentLength, data.Length - bodyStart))
                    : string.Empty;
            }

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

        private static async Task<string> ReadChunkedBodyAsync(
            NetworkStream stream,
            ReadOnlyMemory<byte> initialBytes,
            CancellationToken cancellationToken)
        {
            using var encoded = new MemoryStream();
            if (!initialBytes.IsEmpty)
                encoded.Write(initialBytes.Span);

            var buffer = new byte[8192];
            while (encoded.Length <= MaxRequestBodyBytes)
            {
                var data = encoded.ToArray();
                if (TryDecodeChunkedBody(data, out var decoded, out var invalid))
                    return Encoding.UTF8.GetString(decoded);
                if (invalid)
                    return string.Empty;

                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read <= 0)
                    break;
                encoded.Write(buffer, 0, read);
            }

            return string.Empty;
        }

        private static bool TryDecodeChunkedBody(byte[] encoded, out byte[] decoded, out bool invalid)
        {
            using var output = new MemoryStream();
            var offset = 0;
            invalid = false;
            decoded = Array.Empty<byte>();

            while (true)
            {
                var lineEnd = FindCrlf(encoded, offset);
                if (lineEnd < 0)
                    return false;

                var sizeText = Encoding.ASCII.GetString(encoded, offset, lineEnd - offset);
                var extensionIndex = sizeText.IndexOf(';');
                if (extensionIndex >= 0)
                    sizeText = sizeText[..extensionIndex];
                if (!int.TryParse(sizeText.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var chunkSize)
                    || chunkSize < 0
                    || output.Length + chunkSize > MaxRequestBodyBytes)
                {
                    invalid = true;
                    return false;
                }

                offset = lineEnd + 2;
                if (chunkSize == 0)
                {
                    if (encoded.Length < offset + 2)
                        return false;
                    if (encoded[offset] == '\r' && encoded[offset + 1] == '\n')
                    {
                        decoded = output.ToArray();
                        return true;
                    }

                    var trailerEnd = FindHeaderEnd(encoded, encoded.Length, offset);
                    if (trailerEnd < 0)
                        return false;
                    decoded = output.ToArray();
                    return true;
                }

                if (encoded.Length < offset + chunkSize + 2)
                    return false;
                output.Write(encoded, offset, chunkSize);
                offset += chunkSize;
                if (encoded[offset] != '\r' || encoded[offset + 1] != '\n')
                {
                    invalid = true;
                    return false;
                }
                offset += 2;
            }
        }

        private static int FindCrlf(byte[] buffer, int startIndex)
        {
            for (var index = Math.Max(0, startIndex); index <= buffer.Length - 2; index++)
            {
                if (buffer[index] == '\r' && buffer[index + 1] == '\n')
                    return index;
            }
            return -1;
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

        private static int FindHeaderEnd(byte[] buffer, int length, int startIndex = 0)
        {
            for (var index = Math.Max(0, startIndex); index <= length - 4; index++)
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
