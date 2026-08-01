using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotTokenBudgetChatClient : DelegatingChatClient
    {
        private static CopilotTokenUsage ExtractUsage(IEnumerable<AIContent>? contents)
        {
            var usage = CopilotTokenUsage.Empty;
            foreach (var usageContent in contents?.OfType<UsageContent>() ?? Enumerable.Empty<UsageContent>())
            {
                var details = usageContent.Details;
                static int ToInt(long? value) => value.HasValue ? (int)Math.Clamp(value.Value, 0, int.MaxValue) : 0;
                usage = usage.MergeProgress(new CopilotTokenUsage(
                    ToInt(details.InputTokenCount),
                    ToInt(details.OutputTokenCount),
                    ToInt(details.TotalTokenCount),
                    details.CachedInputTokenCount.HasValue
                        ? ToInt(details.CachedInputTokenCount)
                        : null));
            }

            return usage;
        }

        private static int AddClamped(int left, int right)
        {
            return (int)Math.Clamp((long)Math.Max(0, left) + Math.Max(0, right), 0, int.MaxValue);
        }

        private static long AddClamped(long left, long right)
        {
            var normalizedLeft = Math.Max(0, left);
            var normalizedRight = Math.Max(0, right);
            return normalizedLeft > long.MaxValue - normalizedRight
                ? long.MaxValue
                : normalizedLeft + normalizedRight;
        }

        private static long ToMilliseconds(TimeSpan delay)
        {
            return delay <= TimeSpan.Zero
                ? 0
                : (long)Math.Ceiling(delay.TotalMilliseconds);
        }

        private static long ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks <= 0
                ? 0
                : (long)Math.Ceiling(stopwatchTicks * 1000d / Stopwatch.Frequency);
        }

        private static int EstimateTokens(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options,
            long responseWeight)
        {
            var weight = EstimateInputWeight(messages, options) + Math.Max(0, responseWeight);
            return WeightToTokenEstimate(weight);
        }

        private static int EstimateInputTokens(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options)
        {
            return WeightToTokenEstimate(EstimateInputWeight(messages, options));
        }

        internal static int EstimateMessageTokens(Microsoft.Extensions.AI.ChatMessage[] messages)
        {
            return WeightToTokenEstimate(EstimateMessageWeight(messages));
        }

        private static long EstimateInputWeight(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options)
        {
            var weight = EstimateTextWeight(options?.Instructions);
            weight += EstimateToolsWeight(options?.Tools);
            weight += EstimateMessageWeight(messages);
            return weight;
        }

        private static long EstimateMessageWeight(IEnumerable<Microsoft.Extensions.AI.ChatMessage>? messages)
        {
            long weight = 0;
            foreach (var message in messages ?? Enumerable.Empty<Microsoft.Extensions.AI.ChatMessage>())
            {
                weight += 16;
                weight += EstimateContentWeight(message.Contents);
            }
            return weight;
        }

        private static long EstimateContentWeight(IEnumerable<AIContent>? contents)
        {
            long weight = 0;
            foreach (var content in contents ?? Enumerable.Empty<AIContent>())
                weight += EstimateContentWeight(content);
            return weight;
        }

        private static long EstimateContentWeight(AIContent? content)
        {
            return content switch
            {
                null => 0,
                TextContent text => EstimateTextWeight(text.Text),
                TextReasoningContent reasoning => EstimateTextWeight(reasoning.Text) + EstimateTextWeight(reasoning.ProtectedData),
                FunctionCallContent functionCall => EstimateTextWeight(functionCall.CallId)
                    + EstimateTextWeight(functionCall.Name)
                    + EstimateValueWeight(functionCall.Arguments)
                    + EstimateTextWeight(functionCall.Exception?.Message),
                FunctionResultContent functionResult => EstimateTextWeight(functionResult.CallId)
                    + EstimateValueWeight(functionResult.Result)
                    + EstimateTextWeight(functionResult.Exception?.Message),
                ToolApprovalRequestContent approvalRequest => EstimateTextWeight(approvalRequest.RequestId)
                    + EstimateContentWeight(approvalRequest.ToolCall),
                ToolApprovalResponseContent approvalResponse => EstimateTextWeight(approvalResponse.RequestId)
                    + EstimateTextWeight(approvalResponse.Reason)
                    + EstimateContentWeight(approvalResponse.ToolCall),
                ErrorContent error => EstimateTextWeight(error.Message)
                    + EstimateTextWeight(error.ErrorCode)
                    + EstimateTextWeight(error.Details),
                DataContent data => EstimateDataContentWeight(data),
                UriContent uri => EstimateTextWeight(uri.Uri?.OriginalString) + EstimateTextWeight(uri.MediaType),
                _ => EstimateTextWeight(content.ToString()),
            };
        }

        private static long EstimateToolsWeight(IEnumerable<AITool>? tools)
        {
            long weight = 0;
            foreach (var tool in tools ?? Enumerable.Empty<AITool>())
            {
                weight += EstimateTextWeight(tool.Name);
                weight += EstimateTextWeight(tool.Description);
                if (tool is AIFunction function)
                {
                    weight += function.JsonSchema.ValueKind == JsonValueKind.Undefined
                        ? 0
                        : EstimateTextWeight(function.JsonSchema.GetRawText());
                    if (function.ReturnJsonSchema is JsonElement returnSchema
                        && returnSchema.ValueKind != JsonValueKind.Undefined)
                    {
                        weight += EstimateTextWeight(returnSchema.GetRawText());
                    }
                }
            }
            return weight;
        }

        private static long EstimateValueWeight(object? value)
        {
            if (value == null)
                return 4;
            if (value is string text)
                return EstimateTextWeight(text);
            if (value is JsonElement element)
                return EstimateTextWeight(element.GetRawText());

            try
            {
                return EstimateTextWeight(JsonSerializer.Serialize(value, value.GetType()));
            }
            catch (Exception ex) when (ex is NotSupportedException or JsonException)
            {
                return EstimateTextWeight(value.ToString());
            }
        }

        private static long EstimateDataContentWeight(DataContent data)
        {
            var encodedWeight = EstimateEncodedDataWeight(data.Data.Length);
            if (encodedWeight == 0)
                encodedWeight = EstimateTextWeight(data.Uri);
            return encodedWeight + EstimateTextWeight(data.MediaType) + EstimateTextWeight(data.Name);
        }

        private static long EstimateEncodedDataWeight(int byteCount)
        {
            return byteCount <= 0 ? 0 : ((long)byteCount + 2) / 3 * 4;
        }

        private static long EstimateTextWeight(string? value)
            => CopilotTokenEstimator.EstimateTextWeight(value);

        private static int WeightToTokenEstimate(long weight)
            => CopilotTokenEstimator.WeightToTokenEstimate(weight);
    }
}
