#pragma warning disable OPENAI001, SCME0001
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // One instance belongs to one Agent run. The comparison ID is diagnostic metadata,
    // never a conversation pointer, checkpoint, or source of model-visible history.
    internal sealed class CopilotOpenAiPromptCacheChatClient(
        IChatClient innerClient,
        Action<string> onDiagnostic) : DelegatingChatClient(innerClient)
    {
        private string? _completedResponseId;

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var comparisonId = Volatile.Read(ref _completedResponseId);
            var response = await base.GetResponseAsync(messages, PrepareOptions(options, comparisonId), cancellationToken).ConfigureAwait(false);
            ObserveResponse(response.RawRepresentation as ResponseResult, comparisonId);
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var comparisonId = Volatile.Read(ref _completedResponseId);
            await foreach (var update in base.GetStreamingResponseAsync(
                messages, PrepareOptions(options, comparisonId), cancellationToken).ConfigureAwait(false))
            {
                if (update.RawRepresentation is StreamingResponseCompletedUpdate completed)
                    ObserveResponse(completed.Response, comparisonId);
                yield return update;
            }
        }

        private static ChatOptions? PrepareOptions(ChatOptions? options, string? comparisonId)
        {
            var originalFactory = options?.RawRepresentationFactory;
            if (comparisonId == null && originalFactory == null)
                return options;

            var prepared = options?.Clone() ?? new ChatOptions();
            prepared.RawRepresentationFactory = client =>
            {
                var original = originalFactory?.Invoke(client);
                if (original != null && original is not CreateResponseOptions)
                    return original;

                // A caller may reuse the object returned by its factory across invocations.
                var responseOptions = original is CreateResponseOptions originalOptions
                    ? ModelReaderWriter.Read<CreateResponseOptions>(ModelReaderWriter.Write(originalOptions, ModelReaderWriterOptions.Json), ModelReaderWriterOptions.Json)!
                    : new CreateResponseOptions();
                if (comparisonId != null)
                    responseOptions.Patch.Set("$.prompt_cache_options.comparison_response_id"u8, comparisonId);
                return responseOptions;
            };
            return prepared;
        }

        private void ObserveResponse(ResponseResult? response, string? comparisonId)
        {
            if (response?.Status != ResponseStatus.Completed)
                return;

            if (IsResponseId(response.Id))
                Interlocked.Exchange(ref _completedResponseId, response.Id);
            if (comparisonId == null)
                return;

            string diagnostic;
            try
            {
                diagnostic = response.Patch.Contains("$.prompt_cache_diagnostics"u8)
                    ? FormatDiagnostic(response.Patch.GetJson("$.prompt_cache_diagnostics"u8))
                    : UnavailableDiagnostic;
            }
            catch (Exception)
            {
                // Optional diagnostics must never invalidate a completed answer or trigger replay.
                diagnostic = UnavailableDiagnostic;
            }
            CopilotProviderNotificationObserver.Notify(onDiagnostic, diagnostic, "prompt cache diagnostics");
        }

        private static bool IsResponseId(string? id)
        {
            if (id == null || id.Length is < 6 or > 256 || !id.StartsWith("resp_", StringComparison.Ordinal))
                return false;
            foreach (var character in id)
                if (!char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-')
                    return false;
            return true;
        }

        private const string UnavailableDiagnostic = "提示缓存比较：unavailable（无法判断命中或未命中）；实际缓存用量以供应商 usage 为准。";

        private static string FormatDiagnostic(BinaryData json)
        {
            if (json.ToMemory().Length > 4 * 1024)
                return UnavailableDiagnostic;
            using var document = JsonDocument.Parse(json.ToMemory());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
                return UnavailableDiagnostic;

            var type = typeElement.GetString();
            if (type is not ("cache_hit" or "cache_miss" or "comparison_response_not_found"))
                return UnavailableDiagnostic;

            var builder = new StringBuilder("提示缓存比较：").Append(type);
            if (type == "cache_hit")
                builder.Append("（比较未发现缓存损失，不代表全部输入都命中）");
            else if (type == "comparison_response_not_found")
                builder.Append("（比较记录缺失或过期，将使用本次完成响应作为下一次基准）");
            else
            {
                var reason = root.TryGetProperty("reason", out var reasonElement) && reasonElement.ValueKind == JsonValueKind.String
                    ? reasonElement.GetString() : null;
                builder.Append(" · 原因：").Append(reason switch
                {
                    "model_changed" => "model_changed（模型变化）",
                    "prompt_cache_key_changed" => "prompt_cache_key_changed（缓存键变化）",
                    "service_tier_changed" => "service_tier_changed（服务等级变化）",
                    "tools_changed" => "tools_changed（工具定义或顺序变化）",
                    "text_format_changed" => "text_format_changed（输出格式变化）",
                    "reasoning_effort_changed" => "reasoning_effort_changed（推理强度变化）",
                    "verbosity_changed" => "verbosity_changed（回答详细程度变化）",
                    "context_compacted" => "context_compacted（上下文压缩）",
                    "input_changed" => "input_changed（先前输入变化）",
                    _ => "未知原因",
                });
                AppendCount(builder, root, "comparison_reusable_tokens", " · 基准可复用 Token：");
                AppendCount(builder, root, "cache_missed_tokens", " · 估算未复用 Token：");
            }
            return builder.Append("；诊断估算不计入账单，实际缓存用量以供应商 usage 为准。").ToString();
        }

        private static void AppendCount(StringBuilder builder, JsonElement root, string name, string label)
        {
            if (root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt64(out var count) && count >= 0)
                builder.Append(label).Append(count.ToString(CultureInfo.InvariantCulture));
        }
    }
}
