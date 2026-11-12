using System;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // The API otherwise holds each tool parameter back until it is complete, so writing a large file
    // looks like a dead stream and runs into the inactivity timeout. The flag is set on the wire because
    // the SDK only keeps its own schema conversion for tools it builds itself, and is also sent on
    // non-streaming requests, which ignore it, so the tool definitions stay the same within a conversation.
    // Unbuffered input can be invalid JSON or cut off: HarnessToolFunction rejects the former, and
    // CopilotIncompleteToolCallGuardChatClient withholds calls from a max_tokens or refusal stop.
    internal sealed class CopilotAnthropicEagerToolInputHandler : DelegatingHandler
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null && request.RequestUri?.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal) == true)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (TryEnable(body, out var updated))
                    request.Content = new StringContent(updated, Encoding.UTF8, "application/json");
            }
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        internal static bool TryEnable(string body, out string updated)
        {
            updated = body;
            // A finalization request has no tools but can carry the whole conversation.
            if (!body.Contains("\"tools\"", StringComparison.Ordinal))
                return false;

            var root = JsonNode.Parse(body);
            if (root?["tools"] is not JsonArray tools)
                return false;

            var changed = false;
            foreach (var tool in tools)
            {
                // Server tools have no input_schema and reject the field.
                if (tool is JsonObject definition && definition.ContainsKey("input_schema"))
                {
                    definition["eager_input_streaming"] = true;
                    changed = true;
                }
            }
            if (changed)
                updated = root.ToJsonString(SerializerOptions);
            return changed;
        }
    }
}
