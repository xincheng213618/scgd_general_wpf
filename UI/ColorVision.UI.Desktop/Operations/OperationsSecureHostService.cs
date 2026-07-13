using log4net;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsSecureHostService : IDisposable
    {
        private const int MaxRequestBytes = 256 * 1024;
        private static readonly ILog Log = LogManager.GetLogger(typeof(OperationsSecureHostService));
        private readonly object _syncRoot = new();
        private readonly OperationsDeviceRegistry _registry;
        private readonly OperationsServerIdentity _identity;
        private readonly OperationsPairingService _pairing;
        private readonly OperationsWorkStore _workStore;
        private readonly OperationsDiagnosticBundleService _diagnosticBundles;
        private readonly OperationsRelayClientService _relay;
        private Func<object>? _snapshotProvider;
        private CancellationTokenSource? _cts;
        private TcpListener? _listener;
        private Task? _acceptLoop;
        private OperationsSecureApiRouter? _router;

        public OperationsSecureHostService()
        {
            _registry = new OperationsDeviceRegistry();
            _identity = new OperationsServerIdentity();
            _pairing = new OperationsPairingService(_registry);
            _pairing.ClaimsChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
            _workStore = new OperationsWorkStore();
            _workStore.Changed += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
            _diagnosticBundles = new OperationsDiagnosticBundleService(_workStore);
            _relay = new OperationsRelayClientService(_identity.HostId, _workStore);
        }

        public event EventHandler? StateChanged;

        public bool IsRunning { get; private set; }

        public int RunningPort { get; private set; }

        public string LastStatusMessage { get; private set; } = "安全运维通道已关闭。";

        public string HostId => _identity.HostId;

        public string CertificateSha256 => _identity.CertificateSha256;

        public OperationsPairingService Pairing => _pairing;

        public OperationsDeviceRegistry Registry => _registry;

        public OperationsWorkStore WorkStore => _workStore;

        public OperationsDiagnosticBundleService DiagnosticBundles => _diagnosticBundles;

        public OperationsRelayClientService Relay => _relay;

        public void Start(int port, Func<object> snapshotProvider)
        {
            lock (_syncRoot)
            {
                if (IsRunning && RunningPort == port)
                    return;
                StopNoLock();
                try
                {
                    _cts = new CancellationTokenSource();
                    _router = new OperationsSecureApiRouter(_pairing, new OperationsRequestAuthenticator(_registry), _workStore, snapshotProvider);
                    _snapshotProvider = snapshotProvider;
                    _listener = new TcpListener(IPAddress.Any, port);
                    _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _listener.Start();
                    RunningPort = port;
                    IsRunning = true;
                    LastStatusMessage = $"安全运维通道运行中，HTTPS 端口 {port}。";
                    _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
                    _relay.Start(snapshotProvider);
                    Log.Info(LastStatusMessage);
                }
                catch (Exception ex)
                {
                    StopNoLock();
                    LastStatusMessage = $"安全运维通道启动失败：{ex.Message}";
                    Log.Error(LastStatusMessage, ex);
                }
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            lock (_syncRoot)
            {
                StopNoLock();
                LastStatusMessage = "安全运维通道已关闭。";
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public OperationsPairingChallenge CreatePairingChallenge(string endpoint)
        {
            if (!IsRunning)
                throw new InvalidOperationException("The secure Operations channel is not running.");
            return _pairing.CreateChallenge(HostId, endpoint, CertificateSha256);
        }

        public IReadOnlyList<OperationsPairingClaim> GetPendingClaims() => _pairing.GetPendingClaims();

        private void StopNoLock()
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
                _acceptLoop = null;
                _router = null;
                _snapshotProvider = null;
                _relay.Stop();
                RunningPort = 0;
                IsRunning = false;
            }
        }

        public OperationsDiagnosticBundleResult CreateDiagnosticBundle()
        {
            Func<object> provider = _snapshotProvider ?? throw new InvalidOperationException("The Operations host is not running.");
            return _diagnosticBundles.Create(provider);
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
                    Log.Warn("安全运维通道接收连接失败。", ex);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                linkedCts.CancelAfter(TimeSpan.FromSeconds(20));
                try
                {
                    using SslStream ssl = new(client.GetStream(), leaveInnerStreamOpen: false);
                    await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _identity.Certificate,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                            | System.Security.Authentication.SslProtocols.Tls13,
                    }, linkedCts.Token);

                    OperationsSecureRequest? request = await ReadRequestAsync(ssl, linkedCts.Token);
                    OperationsApiResponse response = request == null || _router == null
                        ? CreatePlainError(400, "invalid_request")
                        : _router.Handle(request);
                    await WriteResponseAsync(ssl, response, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Log.Warn("安全运维通道处理请求失败。", ex);
                }
            }
        }

        private static async Task<OperationsSecureRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
        {
            using MemoryStream buffer = new();
            byte[] chunk = new byte[4096];
            int headerEnd = -1;
            while (buffer.Length < MaxRequestBytes)
            {
                int read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
                if (read <= 0)
                    return null;
                buffer.Write(chunk, 0, read);
                headerEnd = FindHeaderEnd(buffer.GetBuffer(), (int)buffer.Length);
                if (headerEnd >= 0)
                    break;
            }
            if (headerEnd < 0)
                return null;

            byte[] buffered = buffer.ToArray();
            string headerText = Encoding.ASCII.GetString(buffered, 0, headerEnd);
            string[] lines = headerText.Split(["\r\n"], StringSplitOptions.None);
            string[] requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestLine.Length != 3 || !requestLine[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
                return null;

            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < lines.Length; index++)
            {
                int separator = lines[index].IndexOf(':');
                if (separator <= 0)
                    continue;
                headers[lines[index][..separator].Trim()] = lines[index][(separator + 1)..].Trim();
            }
            if (headers.TryGetValue("Transfer-Encoding", out string? transferEncoding)
                && !string.IsNullOrWhiteSpace(transferEncoding))
                return null;

            int contentLength = 0;
            if (headers.TryGetValue("Content-Length", out string? contentLengthText)
                && (!int.TryParse(contentLengthText, out contentLength) || contentLength < 0 || contentLength > MaxRequestBytes))
                return null;

            int bodyOffset = headerEnd + 4;
            byte[] body = new byte[contentLength];
            int alreadyBuffered = Math.Min(contentLength, buffered.Length - bodyOffset);
            if (alreadyBuffered > 0)
                Buffer.BlockCopy(buffered, bodyOffset, body, 0, alreadyBuffered);
            int received = alreadyBuffered;
            while (received < contentLength)
            {
                int read = await stream.ReadAsync(body.AsMemory(received, contentLength - received), cancellationToken);
                if (read <= 0)
                    return null;
                received += read;
            }

            string target = requestLine[1];
            int queryIndex = target.IndexOf('?');
            string path = Uri.UnescapeDataString(queryIndex < 0 ? target : target[..queryIndex]);
            Dictionary<string, string> query = ParseQuery(queryIndex < 0 ? string.Empty : target[(queryIndex + 1)..]);
            return new OperationsSecureRequest
            {
                Method = requestLine[0],
                Path = path,
                Headers = headers,
                Query = query,
                Body = body,
            };
        }

        private static async Task WriteResponseAsync(Stream stream, OperationsApiResponse response, CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes(response.Body);
            StringBuilder headers = new();
            headers.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(GetReasonPhrase(response.StatusCode)).Append("\r\n");
            headers.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
            headers.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            headers.Append("Connection: close\r\n");
            foreach (KeyValuePair<string, string> header in response.Headers)
                headers.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            headers.Append("\r\n");
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers.ToString()), cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static int FindHeaderEnd(byte[] bytes, int length)
        {
            for (int index = 0; index <= length - 4; index++)
            {
                if (bytes[index] == 13 && bytes[index + 1] == 10 && bytes[index + 2] == 13 && bytes[index + 3] == 10)
                    return index;
            }
            return -1;
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = pair.IndexOf('=');
                string name = Uri.UnescapeDataString((separator < 0 ? pair : pair[..separator]).Replace('+', ' '));
                string value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
                values[name] = value;
            }
            return values;
        }

        private static OperationsApiResponse CreatePlainError(int statusCode, string code) => new()
        {
            StatusCode = statusCode,
            Body = $"{{\"error\":{{\"code\":\"{code}\"}}}}",
            Headers = new Dictionary<string, string> { ["Cache-Control"] = "no-store" },
        };

        private static string GetReasonPhrase(int statusCode) => statusCode switch
        {
            200 => "OK",
            202 => "Accepted",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            _ => "Error",
        };

        public void Dispose() => Stop();
    }
}
