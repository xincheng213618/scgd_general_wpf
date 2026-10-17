using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.FlowPoi.Client
{
    /// <summary>
    /// Independent .NET Framework 4.8 client for the main program's FlowRunPoi command.
    /// Each invocation owns one TCP connection and never retries an execution.
    /// </summary>
    public sealed class FlowPoiClient
    {
        public const string Command = "FlowRunPoi";
        private readonly string host;
        private readonly int port;

        public FlowPoiClient(string host, int port = 6666)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("必须填写主程序 IP 或主机名。", nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            this.host = host.Trim();
            this.port = port;
        }

        public Task<FlowPoiResponse> RunByNameAsync(string flowName, string serialNumber = null,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(flowName)) throw new ArgumentException("必须填写流程名称。", nameof(flowName));
            return RunAsync(new JObject { ["FlowName"] = flowName.Trim() }, serialNumber, timeout, cancellationToken);
        }

        public Task<FlowPoiResponse> RunByIdAsync(int flowId, string serialNumber = null,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (flowId == 0) throw new ArgumentOutOfRangeException(nameof(flowId), "流程 ID 不能为 0。");
            return RunAsync(new JObject { ["FlowId"] = flowId }, serialNumber, timeout, cancellationToken);
        }

        private async Task<FlowPoiResponse> RunAsync(JObject parameters, string serialNumber,
            TimeSpan? timeout, CancellationToken cancellationToken)
        {
            TimeSpan duration = timeout ?? TimeSpan.FromMinutes(10);
            if (duration <= TimeSpan.Zero || duration.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(timeout), "超时必须大于 0 且不超过 Int32.MaxValue 毫秒。");
            cancellationToken.ThrowIfCancellationRequested();

            string messageId = Guid.NewGuid().ToString("N");
            var request = new JObject
            {
                ["Version"] = "1.0",
                ["MsgID"] = messageId,
                ["EventName"] = Command,
                ["SerialNumber"] = serialNumber ?? string.Empty,
                // The host contract requires a JSON string inside Params.
                ["Params"] = parameters.ToString(Formatting.None)
            };
            byte[] payload = new UTF8Encoding(false, true).GetBytes(request.ToString(Formatting.None));

            using (var deadline = new CancellationTokenSource(duration))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token))
            using (var tcp = new TcpClient())
            {
                // .NET Framework's ConnectAsync and StreamReader cannot always
                // cancel pending I/O. Closing this request's socket unblocks both.
                using (linked.Token.Register(() => tcp.Close()))
                {
                    try
                    {
                        tcp.NoDelay = true;
                        await tcp.ConnectAsync(host, port).ConfigureAwait(false);
                        using (NetworkStream stream = tcp.GetStream())
                        {
                            await stream.WriteAsync(payload, 0, payload.Length, linked.Token).ConfigureAwait(false);
                            using (var text = new StreamReader(stream, new UTF8Encoding(false, true), true, 8192, true))
                            using (var reader = new JsonTextReader(text)
                            {
                                SupportMultipleContent = true,
                                DateParseHandling = DateParseHandling.None,
                                MaxDepth = 64,
                                CloseInput = false
                            })
                            {
                                // JsonTextReader handles segmented UTF-8, strings
                                // containing braces, and multiple coalesced objects.
                                while (await reader.ReadAsync(linked.Token).ConfigureAwait(false))
                                {
                                    if (reader.TokenType == JsonToken.Comment) continue;
                                    if (reader.TokenType != JsonToken.StartObject)
                                        throw new InvalidDataException("收到的响应不是 JSON 对象。");
                                    JObject json = await JObject.LoadAsync(reader, linked.Token).ConfigureAwait(false);
                                    if (string.IsNullOrEmpty((string)json["EventName"]) || string.IsNullOrEmpty((string)json["MsgID"]))
                                        throw new InvalidDataException("主程序响应缺少 EventName/MsgID。Code=" + json["Code"]
                                            + "，Msg=" + json["Msg"] + "。请确认主程序版本支持 FlowRunPoi，并启用了 Socket JSON 模式。");
                                    if (!string.Equals((string)json["EventName"], Command, StringComparison.Ordinal)
                                        || !string.Equals((string)json["MsgID"], messageId, StringComparison.Ordinal))
                                        continue;

                                    FlowPoiResponse response = json.ToObject<FlowPoiResponse>();
                                    if (response == null)
                                        throw new InvalidDataException("无法解析 FlowRunPoi 响应。");
                                    if (response.IsSuccess && (response.Data == null || response.Data.BatchId <= 0
                                        || response.Data.Poi == null || response.Data.Poi.Count == 0
                                        || (response.Data.Status != "Succeeded" && response.Data.Status != "SucceededWithWarnings")))
                                        throw new InvalidDataException("主程序返回成功码，但没有完整的成功批次和 POI 结果。");

                                    linked.Token.ThrowIfCancellationRequested();
                                    response.ResponseJson = json.ToString(Formatting.Indented);
                                    return response;
                                }
                            }
                        }
                        throw new EndOfStreamException("连接已关闭，尚未收到本次请求的完整响应；主程序中的流程可能仍在执行。");
                    }
                    catch (Exception ex) when (linked.IsCancellationRequested)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new TimeoutException("等待 FlowRunPoi 响应超时。主程序中的流程可能仍在执行；客户端没有自动重发，请先确认现场状态。", ex);
                    }
                }
            }
        }
    }
}
