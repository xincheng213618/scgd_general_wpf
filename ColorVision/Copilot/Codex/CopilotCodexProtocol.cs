using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // A single reader owns RPC replies and retains interleaved notifications in wire order.
    // Process/stream ownership remains with the app server.
    internal sealed class CopilotCodexProtocol(TextWriter input, CopilotBoundedTextLineReader output)
    {
        private readonly Queue<JsonObject> _notifications = new();
        private int _nextId;

        internal async Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = ++_nextId;
            await SendAsync(new { id, method, @params = parameters }, cancellationToken).ConfigureAwait(false);
            while (true)
            {
                var message = await ReadProtocolMessageAsync(output, cancellationToken).ConfigureAwait(false);
                if (message["method"] == null && message["id"]?.ToString() == id.ToString(CultureInfo.InvariantCulture))
                {
                    if (message["error"] != null)
                        throw new InvalidOperationException($"Codex {method} 请求失败（{message["error"]?["code"]}）。请检查 Codex 登录状态与版本。");
                    return message["result"] ?? new JsonObject();
                }
                _notifications.Enqueue(message);
            }
        }

        internal async Task SendAsync(object message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await input.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        internal Task<JsonObject> ReadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _notifications.TryDequeue(out var message)
                ? Task.FromResult(message)
                : ReadProtocolMessageAsync(output, cancellationToken);
        }

        internal static async Task<JsonObject> ReadProtocolMessageAsync(CopilotBoundedTextLineReader reader, CancellationToken cancellationToken)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException("Codex 返回了无效或过大的协议消息。", exception);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (line == null)
                throw new IOException("本机 Codex 已退出，连接中断。请重新检测或检查 Codex 是否可正常使用。");
            if (JsonNode.Parse(line) is not JsonObject message)
                throw new InvalidOperationException("Codex 返回了无效或过大的协议消息。");
            cancellationToken.ThrowIfCancellationRequested();
            return message;
        }
    }
}
