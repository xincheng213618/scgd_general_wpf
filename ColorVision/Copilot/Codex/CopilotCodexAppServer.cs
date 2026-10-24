using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal interface ICopilotCodexAppServer : IDisposable, IAsyncDisposable
    {
        bool? InitialSignedIn => null;
        Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken);
        Task<JsonObject> ReadAsync(CancellationToken cancellationToken);
        ValueTask IAsyncDisposable.DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    // One owner, one stdout reader. No shell, listening port, credential copying, or global config writes.
    internal sealed class CopilotCodexAppServer : ICopilotCodexAppServer
    {
        internal const int MaximumProtocolMessageCharacters = 16 * 1024 * 1024;
        private readonly Process _process;
        private readonly StreamWriter _stdinWriter;
        private readonly CopilotBoundedTextLineReader _stdoutReader;
        private readonly StreamReader _stderrReader;
        private readonly CopilotCodexProtocol _protocol;
        private readonly CopilotCodexOwnedProcessLifetime? _processLifetime;
        private readonly object _disposeGate = new();
        private Task? _disposeTask;
        internal string ExecutablePath { get; }
        internal string Version { get; private set; } = string.Empty;
        public bool? InitialSignedIn { get; private set; }

        private CopilotCodexAppServer(string executable)
        {
            ExecutablePath = executable;
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetTempPath(),
            };
            start.ArgumentList.Add("app-server");
            start.ArgumentList.Add("--listen");
            start.ArgumentList.Add("stdio://");
            // ColorVision remains the only tool executor and approval owner. Codex supplies inference.
            foreach (var setting in IsolationSettings)
            {
                start.ArgumentList.Add("-c");
                start.ArgumentList.Add(setting);
            }
            _process = new Process { StartInfo = start };
            try
            {
                _process.Start();
                _stdinWriter = _process.StandardInput;
                _processLifetime = new CopilotCodexOwnedProcessLifetime(_process, _stdinWriter);
                _stdinWriter.AutoFlush = true;
                _stdoutReader = new CopilotBoundedTextLineReader(
                    _process.StandardOutput, MaximumProtocolMessageCharacters, "Codex protocol");
                _protocol = new CopilotCodexProtocol(_stdinWriter, _stdoutReader);
                // Drain stderr without retaining configuration, account details or other sensitive output.
                _stderrReader = _process.StandardError;
                CopilotCancellationBoundary.ObserveLateFault(DrainStandardErrorAsync(_stderrReader.BaseStream));
            }
            catch
            {
                if (_processLifetime == null)
                {
                    try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
                    catch (Exception exception) when (CopilotCodexOwnedProcessLifetime.IsExpectedProcessFailure(exception)) { }
                }
                try { Dispose(); }
                catch { }
                throw;
            }
        }

        internal static readonly string[] IsolationSettings =
        [
            "mcp_servers={}", "plugins={}", "notify=[]", "project_doc_max_bytes=0",
            "features.shell_tool=false", "features.unified_exec=false", "features.apply_patch_freeform=false",
            "features.code_mode.enabled=false", "features.apps=false", "features.multi_agent=false",
            "features.plugins=false", "features.remote_plugin=false",
            "features.skill_mcp_dependency_install=false", "features.shell_snapshot=false",
            "features.js_repl=false", "features.hooks=false", "web_search=\"disabled\"",
            "sandbox_mode=\"read-only\"", "approval_policy=\"on-request\"",
        ];

        internal static async Task<CopilotCodexAppServer> ConnectAsync(CancellationToken cancellationToken, IReadOnlyList<string>? candidates = null)
        {
            candidates ??= await Task.Run(() => CopilotCodexRuntimeLocator.FindCandidates(), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (candidates.Count == 0)
                throw new InvalidOperationException("未找到本机 Codex。请安装并启动 Codex 桌面版，或继续使用 API Key。");
            // Supplied candidates avoid discovery; process startup still belongs off the UI thread.
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopilotCodexAppServer? server = null;
                try
                {
                    server = new CopilotCodexAppServer(candidate);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    var result = await server.RequestAsync("initialize", new
                    {
                        clientInfo = new { name = "colorvision", version = "1.0" },
                        capabilities = new { experimentalApi = true },
                    }, timeout.Token).ConfigureAwait(false);
                    server.Version = result["userAgent"]?.GetValue<string>() ?? "Codex";
                    await server.SendAsync(new { method = "initialized" }, timeout.Token).ConfigureAwait(false);
                    var account = await server.RequestAsync("account/read", new { refreshToken = false }, timeout.Token).ConfigureAwait(false);
                    server.InitialSignedIn = account["account"] != null;
                    if (server.InitialSignedIn == true)
                    {
                        // Discover capability, not a pinned version: skip older installed runtimes that
                        // cannot carry ColorVision's history. This does not start a model turn.
                        var thread = await server.RequestAsync("thread/start", new
                        {
                            ephemeral = true, sandbox = "read-only", approvalPolicy = "on-request",
                            baseInstructions = "ColorVision compatibility check. No turn will be started.",
                            dynamicTools = Array.Empty<object>(),
                        }, timeout.Token).ConfigureAwait(false);
                        await server.RequestAsync("thread/inject_items", new
                        {
                            threadId = thread["thread"]?["id"]?.GetValue<string>(),
                            items = new[] { new { type = "message", role = "user", content = new[] { new { type = "input_text", text = "ColorVision protocol compatibility check." } } } },
                        }, timeout.Token).ConfigureAwait(false);
                    }
                    return server;
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    if (server != null) await server.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    if (server != null) await server.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("检测到 Codex，但 App Server 无法启动或版本不兼容。请更新并启动 Codex 后重新检测，或使用 API Key。");
        }

        public Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken) =>
            _protocol.RequestAsync(method, parameters, cancellationToken);

        internal Task SendAsync(object message, CancellationToken cancellationToken) =>
            _protocol.SendAsync(message, cancellationToken);

        public Task<JsonObject> ReadAsync(CancellationToken cancellationToken) =>
            _protocol.ReadAsync(cancellationToken);

        internal static async Task DrainStandardErrorAsync(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            var buffer = new byte[4096];
            try
            {
                while (await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) != 0)
                {
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
            {
            }
        }

        public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

        public ValueTask DisposeAsync()
        {
            lock (_disposeGate)
                return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }

        private async Task DisposeCoreAsync()
        {
            try
            {
                if (_processLifetime != null)
                    await _processLifetime.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                try { _stdoutReader?.Dispose(); }
                finally
                {
                    try { _stderrReader?.Dispose(); }
                    finally { _process.Dispose(); }
                }
            }
        }
    }
}
