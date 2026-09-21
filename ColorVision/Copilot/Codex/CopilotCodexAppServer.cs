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
    internal interface ICopilotCodexAppServer : IDisposable
    {
        Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken);
        Task<JsonObject> ReadAsync(CancellationToken cancellationToken);
    }

    // One owner, one stdout reader. No shell, listening port, credential copying, or global config writes.
    internal sealed class CopilotCodexAppServer : ICopilotCodexAppServer
    {
        private readonly Process _process;
        private readonly Queue<JsonObject> _notifications = new();
        private int _nextId;
        private bool _disposed;
        internal string ExecutablePath { get; }
        internal string Version { get; private set; } = string.Empty;

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
                _process.StandardInput.AutoFlush = true;
                // Drain stderr without retaining configuration, account details or other sensitive output.
                _process.ErrorDataReceived += (_, _) => { };
                _process.BeginErrorReadLine();
            }
            catch { _process.Dispose(); throw; }
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

        internal static async Task<CopilotCodexAppServer> ConnectAsync(CancellationToken cancellationToken)
        {
            var candidates = await Task.Run(() => CopilotCodexRuntimeLocator.FindCandidates(), cancellationToken).ConfigureAwait(false);
            if (candidates.Count == 0)
                throw new InvalidOperationException("未找到本机 Codex。请安装并启动 Codex 桌面版，或继续使用 API Key。");
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
                    if (account["account"] != null)
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
                    server?.Dispose();
                }
                catch { server?.Dispose(); throw; }
            }
            throw new InvalidOperationException("检测到 Codex，但 App Server 无法启动或版本不兼容。请更新并启动 Codex 后重新检测，或使用 API Key。");
        }

        public async Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken)
        {
            var id = ++_nextId;
            await SendAsync(new { id, method, @params = parameters }, cancellationToken).ConfigureAwait(false);
            while (true)
            {
                var message = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (message["method"] == null && message["id"]?.ToString() == id.ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    if (message["error"] != null)
                        throw new InvalidOperationException($"Codex {method} 请求失败（{message["error"]?["code"]}）。请检查 Codex 登录状态与版本。");
                    return message["result"] ?? new JsonObject();
                }
                _notifications.Enqueue(message);
            }
        }

        internal Task SendAsync(object message, CancellationToken cancellationToken) =>
            _process.StandardInput.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(message).AsMemory(), cancellationToken);

        public Task<JsonObject> ReadAsync(CancellationToken cancellationToken) =>
            _notifications.TryDequeue(out var message) ? Task.FromResult(message) : ReadLineAsync(cancellationToken);

        private async Task<JsonObject> ReadLineAsync(CancellationToken cancellationToken)
        {
            var line = await _process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
                throw new IOException("本机 Codex 已退出，连接中断。请重新检测或检查 Codex 是否可正常使用。");
            if (line.Length > 16 * 1024 * 1024 || JsonNode.Parse(line) is not JsonObject message)
                throw new InvalidOperationException("Codex 返回了无效或过大的协议消息。");
            return message;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _process.StandardInput.Close();
                if (!_process.WaitForExit(500)) _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch (IOException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { _process.Dispose(); }
        }
    }
}
