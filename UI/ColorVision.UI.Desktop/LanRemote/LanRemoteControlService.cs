using log4net;
using ColorVision.UI.Desktop.Operations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.UI.Desktop.LanRemote
{
    public sealed class LanRemoteControlService : IDisposable, IConfigReloadParticipant
    {
        private const int MaxRequestBytes = 256 * 1024;
        private static readonly ILog Log = LogManager.GetLogger(typeof(LanRemoteControlService));
        private static readonly Lazy<LanRemoteControlService> LazyInstance = new(() => new LanRemoteControlService());
        private static readonly string[] HeaderLineSeparators = { "\r\n" };

        private readonly object _syncRoot = new();
        private CancellationTokenSource? _cts;
        private TcpListener? _listener;
        private Task? _acceptLoopTask;
        private int _runningPort;
        private bool _isRunning;
        private string _lastStatusMessage = "局域网控制已关闭。";
        private DateTime? _startedAt;
        private readonly Func<IPAddress, int, TcpListener> _listenerFactory;
        private readonly Func<ILanRemoteOperationsHost> _operationsHostFactory;
        private readonly Func<IReadOnlyList<string>> _localAddressProvider;
        private readonly Action? _requestSnapshotCaptured;
        private readonly Func<CancellationTokenSource> _cancellationTokenSourceFactory;
        private ILanRemoteOperationsHost? _operationsHost;
        private LanRemoteRuntimeSnapshot _runtimeSnapshot = new();
        private bool _runtimeTransitionInProgress;

        private LanRemoteControlService()
            : this(
                (address, port) => new TcpListener(address, port),
                () => new LanRemoteOperationsHost(new OperationsSecureHostService()),
                GetLocalIpAddresses)
        {
        }

        internal LanRemoteControlService(
            Func<IPAddress, int, TcpListener> listenerFactory,
            Func<ILanRemoteOperationsHost> operationsHostFactory,
            Func<IReadOnlyList<string>> localAddressProvider,
            Action? requestSnapshotCaptured = null,
            Func<CancellationTokenSource>? cancellationTokenSourceFactory = null)
        {
            _listenerFactory = listenerFactory ?? throw new ArgumentNullException(nameof(listenerFactory));
            _operationsHostFactory = operationsHostFactory ?? throw new ArgumentNullException(nameof(operationsHostFactory));
            _localAddressProvider = localAddressProvider ?? throw new ArgumentNullException(nameof(localAddressProvider));
            _requestSnapshotCaptured = requestSnapshotCaptured;
            _cancellationTokenSourceFactory = cancellationTokenSourceFactory ?? (() => new CancellationTokenSource());
        }

        public static LanRemoteControlService Instance => LazyInstance.Value;

        public event EventHandler? StateChanged;

        public bool IsRunning => GetCurrentRuntimeSnapshot().IsRunning;

        public string LastStatusMessage => GetCurrentRuntimeSnapshot().StatusMessage;

        public DateTime? StartedAt => GetCurrentRuntimeSnapshot().StartedAt;

        public OperationsSecureHostService OperationsHost
        {
            get
            {
                lock (_syncRoot)
                {
                    return _operationsHost?.PublicService
                        ?? throw new InvalidOperationException("The secure Operations host has not been initialized by BindCurrentConfig.");
                }
            }
        }

        public string ConfigReloadName => nameof(LanRemoteControlService);

        public int ConfigReloadOrder => 300;

        public void ApplyConfig()
        {
            ApplyCurrentConfig(ConfigHandler.GetInstance(), throwOnStartFailure: false);
        }

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            ApplyCurrentConfig(currentConfig, throwOnStartFailure: true);
        }

        private void ApplyCurrentConfig(IConfigService currentConfig, bool throwOnStartFailure)
        {
            ArgumentNullException.ThrowIfNull(currentConfig);

            LanRemoteRuntimeSettings settings;
            try
            {
                var config = currentConfig.GetRequiredService<LanRemoteControlConfig>();
                if (config.EnsureInitialized())
                    currentConfig.Save<LanRemoteControlConfig>();

                settings = new LanRemoteRuntimeSettings
                {
                    Enabled = config.IsEnabled,
                    Port = config.Port,
                    SecurePort = config.SecurePort,
                    Host = ResolvePreferredLanAddress(config.PreferredHost, _localAddressProvider()),
                    PairingToken = config.PairingToken,
                };
            }
            catch (Exception ex)
            {
                const string statusMessage = "局域网控制配置绑定失败，服务已安全关闭。";
                lock (_syncRoot)
                {
                    StopNoLock(statusMessage, settings: new LanRemoteRuntimeSettings());
                }
                Log.Error(statusMessage, ex);
                if (throwOnStartFailure)
                    throw new InvalidOperationException(statusMessage, ex);
                return;
            }

            lock (_syncRoot)
            {
                try
                {
                    // SettingsControl expects the Operations owner after the initial bind even when LAN control is disabled.
                    // Constructing it here keeps certificate/APPDATA work and any failure inside participant isolation.
                    GetOrCreateOperationsHostNoLock();
                }
                catch (Exception ex)
                {
                    string statusMessage = $"局域网控制启动失败：安全通道初始化失败：{ex.Message}";
                    StopNoLock(statusMessage, settings: settings);
                    Log.Error(statusMessage, ex);
                    if (throwOnStartFailure)
                        throw new InvalidOperationException(statusMessage, ex);
                    return;
                }

                if (!settings.Enabled)
                {
                    StopNoLock("局域网控制已关闭。", settings: settings);
                    return;
                }

                if (_isRunning
                    && _runningPort == settings.Port
                    && _operationsHost is { IsRunning: true } operationsHost
                    && operationsHost.RunningPort == settings.SecurePort)
                {
                    _lastStatusMessage = $"局域网控制运行中：{GetBaseUrl(settings)}";
                    PublishRuntimeSnapshotNoLock(settings);
                    PublishStateChanged();
                    return;
                }

                StartNoLock(settings);
                if (throwOnStartFailure && !_isRunning)
                    throw new InvalidOperationException(_lastStatusMessage);
            }
        }

        public void Stop()
        {
            lock (_syncRoot)
            {
                StopNoLock("局域网控制已关闭。");
            }
        }

        public string GetConnectionUrl()
        {
            LanRemoteRuntimeSettings settings = GetCurrentRuntimeSnapshot().Settings;
            return BuildConnectionUrl(settings.Host, settings.Port, settings.PairingToken);
        }

        public string GetBaseUrl()
        {
            return GetBaseUrl(GetCurrentRuntimeSnapshot().Settings);
        }

        public string GetSecureBaseUrl()
        {
            return GetSecureBaseUrl(GetCurrentRuntimeSnapshot().Settings);
        }

        public string CreateSecurePairingPayload()
        {
            lock (_syncRoot)
            {
                LanRemoteRuntimeSettings settings = GetCurrentRuntimeSnapshot().Settings;
                return (_operationsHost ?? throw new InvalidOperationException("The secure Operations host has not been initialized."))
                    .CreatePairingPayload(GetSecureBaseUrl(settings));
            }
        }

        public static IReadOnlyList<string> GetLocalIpAddresses()
        {
            return GetLanAddressCandidates()
                .Select(item => item.Address)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private void StartNoLock(LanRemoteRuntimeSettings settings)
        {
            _runtimeTransitionInProgress = true;
            StopResourcesNoLock();
            _lastStatusMessage = "局域网控制正在重启。";
            _runningPort = settings.Port;
            _startedAt = DateTime.Now;
            PublishRuntimeSnapshotNoLock(settings);

            try
            {
                _cts = _cancellationTokenSourceFactory()
                    ?? throw new InvalidOperationException("The cancellation-token-source factory returned null.");
                _listener = _listenerFactory(IPAddress.Any, settings.Port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Start();

                ILanRemoteOperationsHost operationsHost = _operationsHost
                    ?? throw new InvalidOperationException("The secure Operations host has not been initialized.");
                operationsHost.Start(
                    settings.SecurePort,
                    () => BuildStatusPayload(GetCurrentRuntimeSnapshot()));
                if (!operationsHost.IsRunning || operationsHost.RunningPort != settings.SecurePort)
                {
                    string operationsStatus = string.IsNullOrWhiteSpace(operationsHost.LastStatusMessage)
                        ? "安全通道未进入运行状态。"
                        : operationsHost.LastStatusMessage;
                    throw new InvalidOperationException(
                        $"安全通道启动失败，预期端口 {settings.SecurePort}，实际端口 {operationsHost.RunningPort}：{operationsStatus}");
                }

                _isRunning = true;
                _lastStatusMessage = $"局域网控制运行中：{GetBaseUrl(settings)}";
                PublishRuntimeSnapshotNoLock(settings);
                Log.Info(_lastStatusMessage);
                CancellationToken cancellationToken = _cts.Token;
                _acceptLoopTask = Task.Run(() => AcceptLoopAsync(cancellationToken));
            }
            catch (SocketException ex)
            {
                StopResourcesNoLock();
                _lastStatusMessage = $"局域网控制启动失败，端口 {settings.Port} 不可用：{ex.Message}";
                Log.Error(_lastStatusMessage, ex);
            }
            catch (Exception ex)
            {
                StopResourcesNoLock();
                _lastStatusMessage = $"局域网控制启动失败：{ex.Message}";
                Log.Error(_lastStatusMessage, ex);
            }
            finally
            {
                _runtimeTransitionInProgress = false;
                PublishRuntimeSnapshotNoLock(settings);
                PublishStateChanged();
            }
        }

        private void StopNoLock(
            string statusMessage,
            bool publish = true,
            LanRemoteRuntimeSettings? settings = null)
        {
            _runtimeTransitionInProgress = true;
            StopResourcesNoLock();
            _lastStatusMessage = statusMessage;
            _runtimeTransitionInProgress = false;

            PublishRuntimeSnapshotNoLock(settings ?? GetCurrentRuntimeSnapshot().Settings);

            if (publish)
                PublishStateChanged();
        }

        private void StopResourcesNoLock()
        {
            try
            {
                _cts?.Cancel();
            }
            catch (Exception ex)
            {
                Log.Warn("局域网控制取消运行任务失败，继续关闭监听器。", ex);
            }

            try
            {
                _listener?.Stop();
            }
            catch (Exception ex)
            {
                Log.Warn("局域网控制关闭 HTTP 监听器失败，继续关闭安全通道。", ex);
            }

            try
            {
                _operationsHost?.Stop();
            }
            catch (Exception ex)
            {
                Log.Warn("局域网控制关闭安全通道失败。", ex);
            }
            finally
            {
                _listener = null;
                _cts?.Dispose();
                _cts = null;
                _acceptLoopTask = null;
                _runningPort = 0;
                _isRunning = false;
                _startedAt = null;
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
                    // The handler owns the accepted socket even if the listener token is cancelled
                    // between AcceptTcpClientAsync and Task.Run. A cancelled Task.Run token can skip
                    // the delegate entirely and leak the socket.
                    TcpClient acceptedClient = client;
                    client = null;
                    _ = Task.Run(() => HandleClientAsync(acceptedClient, cancellationToken), CancellationToken.None);
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
                    Log.Warn("局域网控制接收连接失败。", ex);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using var _ = client;
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(20));
                var stream = client.GetStream();
                var callerSource = client.Client.RemoteEndPoint?.ToString() ?? string.Empty;
                LanHttpRequest? request = await ReadRequestAsync(stream, callerSource, linkedCts.Token);
                LanRemoteRuntimeSnapshot runtimeSnapshot = GetCurrentRuntimeSnapshot();
                _requestSnapshotCaptured?.Invoke();
                LanHttpResponse response = request == null
                    ? PlainText(400, "Invalid HTTP request.")
                    : HandleRequest(request, runtimeSnapshot);

                await WriteResponseAsync(stream, response, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Warn("局域网控制处理请求失败。", ex);
            }
        }

        private static LanHttpResponse HandleRequest(LanHttpRequest request, LanRemoteRuntimeSnapshot runtimeSnapshot)
        {
            LanRemoteRuntimeSettings settings = runtimeSnapshot.Settings;

            if (string.Equals(request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                return new LanHttpResponse { StatusCode = 204 };

            if (request.Path == "/" || request.Path.Equals("/mobile", StringComparison.OrdinalIgnoreCase))
                return Html(200, BuildMobilePage(request, settings));

            if (request.Path.Equals("/api/status", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsTokenValid(request, settings))
                    return Json(401, new { ok = false, error = "unauthorized" });

                return Json(200, BuildStatusPayload(runtimeSnapshot));
            }

            if (request.Path.Equals("/api/logs", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsTokenValid(request, settings))
                    return Json(401, new { ok = false, error = "unauthorized" });

                int count = 40;
                if (request.Query.TryGetValue("count", out string? countText)
                    && int.TryParse(countText, out int requestedCount))
                {
                    count = Math.Clamp(requestedCount, 1, 120);
                }

                return Json(200, new
                {
                    ok = true,
                    lines = ReadRecentLogLines(count),
                    serverTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            if (request.Path.Equals("/api/command", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsTokenValid(request, settings))
                    return Json(401, new { ok = false, error = "unauthorized" });

                return HandleCommandRequest(request, runtimeSnapshot);
            }

            if (request.Path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
                return new LanHttpResponse { StatusCode = 204 };

            return PlainText(404, "Not Found");
        }

        private static string BuildMobilePage(LanHttpRequest request, LanRemoteRuntimeSettings settings)
        {
            bool authorized = IsTokenValid(request, settings);
            string statusText = authorized ? "已连接到电脑端" : "二维码已失效，请在电脑端重新生成。";
            string token = EscapeJavaScript(GetTokenFromRequest(request) ?? string.Empty);
            string machineName = EscapeHtml(Environment.MachineName);
            string endpoint = EscapeHtml(GetBaseUrl(settings));

            return $$"""
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>ColorVision 局域网控制</title>
<style>
body{margin:0;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;background:#f2f7ff;color:#182033}
.page{min-height:100vh;padding:calc(env(safe-area-inset-top) + 26px) 18px calc(env(safe-area-inset-bottom) + 24px);box-sizing:border-box}
.top{display:flex;align-items:center;justify-content:space-between;gap:18px;margin:0 auto 28px;width:min(560px,100%)}
.brand{margin:0;color:#1598cc;font-size:34px;line-height:1;font-weight:800;letter-spacing:.2px}
.subtitle{margin:8px 0 0;color:#60708b;font-size:15px}
.badge{flex:none;border-radius:999px;padding:9px 12px;background:#eaf7ef;color:#17663a;font-size:13px;font-weight:700}
.badge.bad{background:#fff0ef;color:#a12a20}
.content{width:min(560px,100%);margin:0 auto;display:grid;gap:14px}
.card{background:white;border:1px solid #dde4ef;border-radius:18px;overflow:hidden;box-shadow:0 12px 34px rgba(24,32,51,.08)}
.card-title{margin:0;padding:18px 18px 6px;font-size:20px;line-height:1.25}
.hint{margin:0;padding:0 18px 14px;color:#66758f;font-size:14px;line-height:1.55}
.list{list-style:none;margin:0;padding:0}
.item{display:flex;align-items:center;justify-content:space-between;gap:16px;min-height:54px;padding:0 18px;border-top:1px solid #edf1f6;font-size:15px;box-sizing:border-box}
.item:first-child{border-top:0}
.label{color:#66758f}
.value{text-align:right;word-break:break-all;font-weight:650;color:#243044}
.action{width:100%;border:0;background:white;color:#182033;text-align:left;font:inherit;cursor:pointer}
.action .value{color:#1f6feb}
.action:active{background:#eef6ff}
.logs .item{align-items:flex-start;padding-top:12px;padding-bottom:12px;font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:12px;line-height:1.45;color:#60708b}
.empty{padding:18px;color:#66758f;font-size:14px}
</style>
</head>
<body>
<main class="page">
<header class="top">
<div>
<h1 class="brand">ColorVision</h1>
<p class="subtitle">局域网控制列表</p>
</div>
<div class="badge {{(authorized ? "" : "bad")}}" id="authBadge">{{EscapeHtml(statusText)}}</div>
</header>
<section class="content">
<article class="card">
<h2 class="card-title">电脑状态</h2>
<p class="hint">当前连接：{{machineName}} · {{endpoint}}</p>
<ul class="list" id="statusList">
<li class="item"><span class="label">状态</span><span class="value">正在刷新</span></li>
</ul>
</article>
<article class="card">
<h2 class="card-title">快捷操作</h2>
<ul class="list" id="actionList">
<li class="empty">正在加载操作列表...</li>
</ul>
</article>
<article class="card logs">
<h2 class="card-title">最近日志</h2>
<ul class="list" id="logList">
<li class="empty">正在读取日志...</li>
</ul>
</article>
</section>
</main>
<script>
const token = "{{token}}";
const statusList = document.getElementById("statusList");
const actionList = document.getElementById("actionList");
const logList = document.getElementById("logList");

function text(value, fallback = "--") {
  return value === undefined || value === null || value === "" ? fallback : String(value);
}

function escapeHtml(value) {
  return text(value, "").replace(/[&<>"']/g, ch => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;"
  })[ch]);
}

function row(label, value) {
  return `<li class="item"><span class="label">${escapeHtml(label)}</span><span class="value">${escapeHtml(value)}</span></li>`;
}

async function refreshStatus(){
  try {
    const res = await fetch("/api/status?token=" + encodeURIComponent(token));
    const data = await res.json();
    renderStatus(data);
    await refreshLogs();
  } catch (e) {
    statusList.innerHTML = row("状态", "无法连接");
    logList.innerHTML = `<li class="empty">无法读取日志。</li>`;
  }
}

function renderStatus(data) {
  statusList.innerHTML = [
    row("状态", data.ok ? "在线 " + text(data.serverTime, "") : "未授权"),
    row("主窗口", data.mainWindow ? `${text(data.mainWindow.state)} · ${data.mainWindow.isVisible ? "可见" : "不可见"}` : "--"),
    row("版本", data.version),
    row("在线时间", formatDuration(data.uptimeSeconds || 0)),
    row("进程内存", data.process ? text(data.process.memoryMb) + " MB" : "--"),
    row("可用地址", Array.isArray(data.addresses) ? data.addresses.join(", ") : "--")
  ].join("");
  renderActions(data.capabilities || []);
}

function renderActions(capabilities) {
  const items = [
    { label: "刷新状态", type: "refresh" },
    ...capabilities.filter(item => item.action !== "refreshStatus").map(item => ({ label: item.label, action: item.action })),
    { label: "管理连接", href: "cvapp://connections" },
    { label: "断开此电脑", href: "cvapp://disconnect" }
  ];
  actionList.innerHTML = items.map(item => {
    if (item.href) {
      return `<li class="item action" onclick="location.href='${item.href}'"><span>${escapeHtml(item.label)}</span><span class="value">›</span></li>`;
    }
    const call = item.type === "refresh" ? "refreshStatus()" : `sendCommand('${item.action}')`;
    return `<li class="item action" onclick="${call}"><span>${escapeHtml(item.label)}</span><span class="value">›</span></li>`;
  }).join("");
}

async function sendCommand(action) {
  const res = await fetch("/api/command?token=" + encodeURIComponent(token), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ action })
  });
  const data = await res.json();
  if (data.status) {
    renderStatus(data.status);
  } else {
    await refreshStatus();
  }
}

async function refreshLogs() {
  const res = await fetch("/api/logs?count=12&token=" + encodeURIComponent(token));
  const data = await res.json();
  const lines = Array.isArray(data.lines) ? data.lines : [];
  logList.innerHTML = lines.length
    ? lines.map(line => `<li class="item">${escapeHtml(line)}</li>`).join("")
    : `<li class="empty">暂无日志。</li>`;
}

function formatDuration(seconds) {
  const total = Math.max(0, Number(seconds) || 0);
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const rest = total % 60;
  if (hours > 0) return `${hours}小时 ${minutes}分钟`;
  if (minutes > 0) return `${minutes}分钟 ${rest}秒`;
  return `${rest}秒`;
}

refreshStatus();
</script>
</body>
</html>
""";
        }

        private static object BuildStatusPayload(LanRemoteRuntimeSnapshot runtimeSnapshot)
        {
            LanRemoteRuntimeSettings settings = runtimeSnapshot.Settings;
            using Process process = Process.GetCurrentProcess();
            DateTime now = DateTime.Now;
            MainWindowSnapshot window = GetMainWindowSnapshot();
            string selectedAddress = settings.Host;
            IReadOnlyList<string> addresses = GetLocalIpAddresses();
            LanRemoteOperationsHostStatus operationsStatus = runtimeSnapshot.OperationsStatus;

            return new
            {
                ok = true,
                app = "ColorVision",
                machine = Environment.MachineName,
                user = Environment.UserName,
                version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty,
                endpoint = GetBaseUrl(settings),
                selectedAddress,
                addresses,
                port = runtimeSnapshot.RunningPort > 0 ? runtimeSnapshot.RunningPort : settings.Port,
                isRunning = runtimeSnapshot.IsRunning,
                statusMessage = runtimeSnapshot.StatusMessage,
                secureOperations = new
                {
                    isRunning = operationsStatus.IsRunning,
                    endpoint = GetSecureBaseUrl(settings),
                    pairedDeviceCount = operationsStatus.PairedDeviceCount,
                    relayConfigured = operationsStatus.RelayConfigured,
                    relayRunning = operationsStatus.RelayRunning,
                    relayLastHeartbeatAt = operationsStatus.RelayLastHeartbeatAt,
                    relayStatus = operationsStatus.RelayStatus,
                },
                startedAt = runtimeSnapshot.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                uptimeSeconds = runtimeSnapshot.StartedAt.HasValue
                    ? Math.Max(0, (int)(now - runtimeSnapshot.StartedAt.Value).TotalSeconds)
                    : 0,
                serverTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
                os = Environment.OSVersion.VersionString,
                process = new
                {
                    id = Environment.ProcessId,
                    name = process.ProcessName,
                    startedAt = GetProcessStartTime(process),
                    memoryMb = Math.Round(process.WorkingSet64 / 1024d / 1024d, 1)
                },
                mainWindow = window,
                capabilities = new[]
                {
                    new { action = "showMainWindow", label = "显示主窗口" },
                    new { action = "minimizeMainWindow", label = "最小化主窗口" },
                    new { action = "refreshStatus", label = "刷新状态" }
                }
            };
        }

        private static LanHttpResponse HandleCommandRequest(LanHttpRequest request, LanRemoteRuntimeSnapshot runtimeSnapshot)
        {
            string action = GetCommandAction(request);
            if (string.IsNullOrWhiteSpace(action))
                return Json(400, new { ok = false, error = "missing action" });

            string normalized = action.Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim()
                .ToLowerInvariant();

            try
            {
                string message = normalized switch
                {
                    "showmainwindow" or "showwindow" or "show" => ExecuteOnUiThread(ShowMainWindow),
                    "minimizemainwindow" or "minimizewindow" or "minimize" => ExecuteOnUiThread(MinimizeMainWindow),
                    "refreshstatus" or "refresh" => "状态已刷新。",
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(message))
                    return Json(400, new { ok = false, error = "unsupported action", action });

                return Json(200, new
                {
                    ok = true,
                    action,
                    message,
                    status = BuildStatusPayload(runtimeSnapshot)
                });
            }
            catch (Exception ex)
            {
                Log.Warn($"局域网控制命令执行失败：{action}", ex);
                return Json(500, new { ok = false, error = ex.Message, action });
            }
        }

        private static string GetCommandAction(LanHttpRequest request)
        {
            if (request.Query.TryGetValue("action", out string? queryAction) && !string.IsNullOrWhiteSpace(queryAction))
                return queryAction;

            if (string.IsNullOrWhiteSpace(request.Body))
                return string.Empty;

            try
            {
                using JsonDocument document = JsonDocument.Parse(request.Body);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("action", out JsonElement actionElement)
                    && actionElement.ValueKind == JsonValueKind.String)
                {
                    return actionElement.GetString() ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static IReadOnlyList<string> ReadRecentLogLines(int count)
        {
            string? logFile = FindLatestLogFile();
            if (string.IsNullOrWhiteSpace(logFile))
                return Array.Empty<string>();

            try
            {
                string[] lines;
                using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    lines = reader.ReadToEnd()
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                }

                return lines
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .TakeLast(count)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warn("读取局域网控制日志失败。", ex);
                return new[] { $"读取日志失败：{ex.Message}" };
            }
        }

        private static string? FindLatestLogFile()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "log"),
                Path.Combine(Environment.CurrentDirectory, "log")
            };

            return candidates
                .Where(Directory.Exists)
                .SelectMany(directory => Directory.EnumerateFiles(directory, "*.txt", SearchOption.TopDirectoryOnly))
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }

        private static T ExecuteOnUiThread<T>(Func<T> action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                throw new InvalidOperationException("当前没有可用的 WPF 调度器。");

            return dispatcher.CheckAccess()
                ? action()
                : dispatcher.Invoke(action);
        }

        private static string ShowMainWindow()
        {
            Window? window = Application.Current?.MainWindow;
            if (window == null)
                return "当前没有主窗口。";

            if (!window.IsVisible)
                window.Show();

            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
            return "主窗口已显示。";
        }

        private static string MinimizeMainWindow()
        {
            Window? window = Application.Current?.MainWindow;
            if (window == null)
                return "当前没有主窗口。";

            window.WindowState = WindowState.Minimized;
            return "主窗口已最小化。";
        }

        private static MainWindowSnapshot GetMainWindowSnapshot()
        {
            try
            {
                return ExecuteOnUiThread(() =>
                {
                    Window? window = Application.Current?.MainWindow;
                    if (window == null)
                        return new MainWindowSnapshot(false, string.Empty, "Unknown", false);

                    return new MainWindowSnapshot(
                        true,
                        window.Title ?? string.Empty,
                        window.WindowState.ToString(),
                        window.IsVisible);
                });
            }
            catch
            {
                return new MainWindowSnapshot(false, string.Empty, "Unknown", false);
            }
        }

        private static string GetProcessStartTime(Process process)
        {
            try
            {
                return process.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsTokenValid(LanHttpRequest request, LanRemoteRuntimeSettings settings)
        {
            string? token = GetTokenFromRequest(request);
            string expected = settings.PairingToken;
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expected))
                return false;

            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            return tokenBytes.Length == expectedBytes.Length
                && CryptographicOperations.FixedTimeEquals(tokenBytes, expectedBytes);
        }

        private static string? GetTokenFromRequest(LanHttpRequest request)
        {
            if (request.Query.TryGetValue("token", out string? token) && !string.IsNullOrWhiteSpace(token))
                return token;

            if (request.Headers.TryGetValue("Authorization", out string? authorization)
                && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authorization["Bearer ".Length..].Trim();
            }

            return request.Headers.TryGetValue("X-ColorVision-Token", out token) ? token : null;
        }

        private static async Task<LanHttpRequest?> ReadRequestAsync(NetworkStream stream, string callerSource, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            using var memory = new MemoryStream();
            int headerEnd = -1;

            while (memory.Length < MaxRequestBytes)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read <= 0)
                    break;

                memory.Write(buffer, 0, read);
                headerEnd = FindHeaderEnd(memory.GetBuffer(), (int)memory.Length);
                if (headerEnd >= 0)
                    break;
            }

            if (headerEnd < 0)
                return null;

            byte[] raw = memory.GetBuffer();
            string headerText = Encoding.ASCII.GetString(raw, 0, headerEnd);
            string[] lines = headerText.Split(HeaderLineSeparators, StringSplitOptions.None);
            if (lines.Length == 0)
                return null;

            string[] requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (requestLine.Length < 2)
                return null;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines.Skip(1))
            {
                int separator = line.IndexOf(':');
                if (separator <= 0)
                    continue;

                headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }

            int contentLength = 0;
            if (headers.TryGetValue("Content-Length", out string? contentLengthText)
                && !int.TryParse(contentLengthText, out contentLength))
            {
                return null;
            }

            if (contentLength > MaxRequestBytes)
                return null;

            int bodyStart = headerEnd + 4;
            int alreadyRead = (int)memory.Length - bodyStart;
            while (alreadyRead < contentLength)
            {
                int remaining = Math.Min(buffer.Length, contentLength - alreadyRead);
                int read = await stream.ReadAsync(buffer.AsMemory(0, remaining), cancellationToken);
                if (read <= 0)
                    break;

                memory.Write(buffer, 0, read);
                alreadyRead += read;
            }

            byte[] data = memory.ToArray();
            string body = contentLength > 0 && data.Length >= bodyStart
                ? Encoding.UTF8.GetString(data, bodyStart, Math.Min(contentLength, data.Length - bodyStart))
                : string.Empty;

            if (!Uri.TryCreate("http://localhost" + requestLine[1], UriKind.Absolute, out Uri? uri))
                return null;

            return new LanHttpRequest
            {
                Method = requestLine[0],
                Path = uri.AbsolutePath,
                Query = ParseQuery(uri.Query),
                Headers = headers,
                Body = body,
                CallerSource = callerSource
            };
        }

        private static async Task WriteResponseAsync(NetworkStream stream, LanHttpResponse response, CancellationToken cancellationToken)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(response.Body ?? string.Empty);
            var builder = new StringBuilder();
            builder.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(GetReasonPhrase(response.StatusCode)).Append("\r\n");
            builder.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
            builder.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            builder.Append("Connection: close\r\n");
            foreach (var header in response.Headers)
                builder.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            builder.Append("\r\n");

            byte[] headerBytes = Encoding.ASCII.GetBytes(builder.ToString());
            await stream.WriteAsync(headerBytes.AsMemory(0, headerBytes.Length), cancellationToken);
            if (bodyBytes.Length > 0)
                await stream.WriteAsync(bodyBytes.AsMemory(0, bodyBytes.Length), cancellationToken);
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query))
                return values;

            string text = query[0] == '?' ? query[1..] : query;
            foreach (string part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = part.Split('=', 2);
                string key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
                string value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace("+", " ")) : string.Empty;
                values[key] = value;
            }

            return values;
        }

        private static LanHttpResponse Json(int statusCode, object body)
        {
            return new LanHttpResponse
            {
                StatusCode = statusCode,
                ContentType = "application/json; charset=utf-8",
                Body = JsonSerializer.Serialize(body)
            };
        }

        private static LanHttpResponse Html(int statusCode, string body)
        {
            return new LanHttpResponse
            {
                StatusCode = statusCode,
                ContentType = "text/html; charset=utf-8",
                Body = body
            };
        }

        private static LanHttpResponse PlainText(int statusCode, string body)
        {
            return new LanHttpResponse
            {
                StatusCode = statusCode,
                ContentType = "text/plain; charset=utf-8",
                Body = body
            };
        }

        private LanRemoteRuntimeSnapshot GetCurrentRuntimeSnapshot()
        {
            return Volatile.Read(ref _runtimeSnapshot);
        }

        private ILanRemoteOperationsHost GetOrCreateOperationsHostNoLock()
        {
            if (_operationsHost != null)
                return _operationsHost;

            ILanRemoteOperationsHost operationsHost = _operationsHostFactory()
                ?? throw new InvalidOperationException("The secure Operations host factory returned null.");
            try
            {
                operationsHost.StateChanged += OperationsHost_StateChanged;
                _operationsHost = operationsHost;
                return operationsHost;
            }
            catch
            {
                try
                {
                    operationsHost.Stop();
                }
                catch (Exception cleanupException)
                {
                    Log.Warn("安全通道事件绑定失败后清理新实例失败。", cleanupException);
                }
                throw;
            }
        }

        private void PublishRuntimeSnapshotNoLock(LanRemoteRuntimeSettings settings)
        {
            LanRemoteOperationsHostStatus operationsStatus;
            try
            {
                operationsStatus = _operationsHost?.CaptureStatus() ?? new LanRemoteOperationsHostStatus();
            }
            catch (Exception ex)
            {
                operationsStatus = new LanRemoteOperationsHostStatus
                {
                    RelayStatus = $"Unable to capture secure Operations status: {ex.Message}",
                };
            }

            Volatile.Write(ref _runtimeSnapshot, new LanRemoteRuntimeSnapshot
            {
                Settings = settings,
                IsRunning = _isRunning,
                RunningPort = _runningPort,
                StatusMessage = _lastStatusMessage,
                StartedAt = _startedAt,
                OperationsStatus = operationsStatus,
            });
        }

        private static string GetBaseUrl(LanRemoteRuntimeSettings settings)
        {
            return $"http://{settings.Host}:{settings.Port}";
        }

        private static string GetSecureBaseUrl(LanRemoteRuntimeSettings settings)
        {
            return $"https://{settings.Host}:{settings.SecurePort}";
        }

        private static string ResolvePreferredLanAddress(
            string preferredHost,
            IReadOnlyList<string> addresses)
        {
            if (!string.IsNullOrWhiteSpace(preferredHost)
                && addresses.Contains(preferredHost, StringComparer.Ordinal))
            {
                return preferredHost;
            }

            return addresses.Count > 0 ? addresses[0] : "127.0.0.1";
        }

        private static string BuildConnectionUrl(string host, int port, string token)
        {
            return $"http://{host}:{port}/mobile?token={Uri.EscapeDataString(token)}";
        }

        private static bool IsUsableLanAddress(string address)
        {
            return !address.StartsWith("127.", StringComparison.Ordinal)
                && !address.StartsWith("169.254.", StringComparison.Ordinal);
        }

        private static bool IsPrivateLanAddress(string address)
        {
            return address.StartsWith("10.", StringComparison.Ordinal)
                || address.StartsWith("192.168.", StringComparison.Ordinal)
                || IsPrivate172Address(address);
        }

        private static bool IsPrivate172Address(string address)
        {
            string[] parts = address.Split('.');
            return parts.Length == 4
                && parts[0] == "172"
                && int.TryParse(parts[1], out int second)
                && second is >= 16 and <= 31;
        }

        private static IReadOnlyList<LanAddressCandidate> GetLanAddressCandidates()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .Where(item => item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(item => item.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(CreateLanAddressCandidates)
                .Where(item => IsUsableLanAddress(item.Address))
                .GroupBy(item => item.Address, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(GetLanAddressScore).First())
                .OrderByDescending(GetLanAddressScore)
                .ThenBy(item => item.Address, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<LanAddressCandidate> CreateLanAddressCandidates(NetworkInterface networkInterface)
        {
            IPInterfaceProperties properties;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch
            {
                yield break;
            }

            bool hasIpv4Gateway = properties.GatewayAddresses.Any(item =>
                item.Address.AddressFamily == AddressFamily.InterNetwork
                && IsUsableLanAddress(item.Address.ToString()));
            bool likelyVirtual = IsLikelyVirtualAdapter(networkInterface);

            foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                yield return new LanAddressCandidate(
                    address.Address.ToString(),
                    networkInterface.NetworkInterfaceType,
                    hasIpv4Gateway,
                    likelyVirtual);
            }
        }

        private static int GetLanAddressScore(LanAddressCandidate candidate)
        {
            int score = 0;

            if (IsPrivateLanAddress(candidate.Address))
                score += 200;

            if (candidate.Address.StartsWith("192.168.", StringComparison.Ordinal))
                score += 120;
            else if (candidate.Address.StartsWith("10.", StringComparison.Ordinal))
                score += 80;
            else if (IsPrivate172Address(candidate.Address))
                score += 40;

            if (candidate.HasIpv4Gateway)
                score += 1000;

            if (candidate.InterfaceType is NetworkInterfaceType.Wireless80211
                or NetworkInterfaceType.Ethernet
                or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetFx
                or NetworkInterfaceType.FastEthernetT)
            {
                score += 240;
            }

            if (candidate.IsLikelyVirtual)
                score -= 1000;

            return score;
        }

        private static bool IsLikelyVirtualAdapter(NetworkInterface networkInterface)
        {
            string text = $"{networkInterface.Name} {networkInterface.Description}".ToLowerInvariant();
            string[] markers =
            {
                "virtual",
                "vethernet",
                "hyper-v",
                "wsl",
                "docker",
                "vmware",
                "virtualbox",
                "vbox",
                "host-only",
                "npcap",
                "tap",
                "tailscale",
                "zerotier",
                "bluetooth",
                "vpn",
                "pseudo"
            };

            return markers.Any(text.Contains);
        }

        private sealed class LanAddressCandidate
        {
            public LanAddressCandidate(
                string address,
                NetworkInterfaceType interfaceType,
                bool hasIpv4Gateway,
                bool isLikelyVirtual)
            {
                Address = address;
                InterfaceType = interfaceType;
                HasIpv4Gateway = hasIpv4Gateway;
                IsLikelyVirtual = isLikelyVirtual;
            }

            public string Address { get; }

            public NetworkInterfaceType InterfaceType { get; }

            public bool HasIpv4Gateway { get; }

            public bool IsLikelyVirtual { get; }
        }

        private sealed class MainWindowSnapshot
        {
            public MainWindowSnapshot(bool exists, string title, string state, bool isVisible)
            {
                Exists = exists;
                Title = title;
                State = state;
                IsVisible = isVisible;
            }

            public bool Exists { get; }

            public string Title { get; }

            public string State { get; }

            public bool IsVisible { get; }
        }

        private static int FindHeaderEnd(byte[] buffer, int length)
        {
            for (int index = 0; index <= length - 4; index++)
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
                204 => "No Content",
                400 => "Bad Request",
                401 => "Unauthorized",
                404 => "Not Found",
                500 => "Internal Server Error",
                503 => "Service Unavailable",
                _ => "OK",
            };
        }

        private static string EscapeHtml(string value)
        {
            return WebUtility.HtmlEncode(value);
        }

        private static string EscapeJavaScript(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OperationsHost_StateChanged(object? sender, EventArgs e)
        {
            lock (_syncRoot)
            {
                if (_runtimeTransitionInProgress)
                    return;

                PublishRuntimeSnapshotNoLock(GetCurrentRuntimeSnapshot().Settings);
            }
            PublishStateChanged();
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_operationsHost != null)
                    _operationsHost.StateChanged -= OperationsHost_StateChanged;
                StopNoLock("局域网控制已关闭。");
            }
        }

        private sealed class LanHttpRequest
        {
            public string Method { get; init; } = string.Empty;
            public string Path { get; init; } = string.Empty;
            public Dictionary<string, string> Query { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public string Body { get; init; } = string.Empty;
            public string CallerSource { get; init; } = string.Empty;
        }

        private sealed class LanHttpResponse
        {
            public int StatusCode { get; init; } = 200;
            public string ContentType { get; init; } = "text/plain; charset=utf-8";
            public string Body { get; init; } = string.Empty;
            public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
