#pragma warning disable MAAI001
#pragma warning disable CA1859
#pragma warning disable OPENAI001
using Anthropic;
using Anthropic.Core;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal static IChatClient CreateChatClient(CopilotProfileConfig profile)
        {
            if (profile.ProviderType == CopilotProviderType.AnthropicCompatible)
            {
                var anthropicClient = new AnthropicClient(new ClientOptions
                {
                    ApiKey = profile.ApiKey,
                    BaseUrl = profile.BaseUrl.Trim().TrimEnd('/'),
                    HttpClient = CopilotProviderHttpTransport.CreateClient(profile.Id),
                });
                return anthropicClient.AsIChatClient(profile.Model, profile.MaxTokens);
            }

            return CopilotOpenAiAgentChatClientFactory.Create(
                profile,
                CopilotProviderHttpTransport.CreateClient(profile.Id));
        }

        internal static ChatOptions BuildChatOptions(CopilotAgentRequest request, IList<AITool> tools)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Profile);
            var profile = request.Profile;
            var options = new ChatOptions
            {
                Instructions = profile.EffectiveSystemPrompt,
                MaxOutputTokens = profile.MaxTokens,
                Temperature = CopilotReasoningRequestMapper.ShouldIncludeTemperature(profile) ? (float)profile.Temperature : null,
                Reasoning = BuildReasoningOptions(profile),
                Tools = tools,
            };
            ApplyCodexReasoningOptions(request, options);
            return options;
        }

        internal static ChatOptions BuildFinalAnswerOptions(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Profile);
            var profile = request.Profile;
            var options = new ChatOptions
            {
                Instructions = profile.EffectiveSystemPrompt
                    + "\n\nYou are the final-answer stage of ColorVision Agent. Business and framework tools are unavailable in this stage. Return only a supported user-facing answer based on the supplied evidence, and explicitly identify incomplete work instead of claiming success.",
                MaxOutputTokens = profile.MaxTokens,
                Temperature = CopilotReasoningRequestMapper.ShouldIncludeTemperature(profile) ? (float)profile.Temperature : null,
                Reasoning = BuildReasoningOptions(profile),
                Tools = Array.Empty<AITool>(),
            };
            ApplyCodexReasoningOptions(request, options);
            return options;
        }

        private static string ExtractFinalAnswerText(ChatResponse response)
        {
            return string.Concat((response?.Messages ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>())
                .SelectMany(message => message.Contents)
                .OfType<TextContent>()
                .Select(content => content.Text));
        }

        private static ReasoningOptions? BuildReasoningOptions(CopilotProfileConfig profile)
        {
            return CopilotReasoningCapabilities.GetEffectiveMode(profile) switch
            {
                CopilotReasoningMode.Disabled => new ReasoningOptions { Effort = ReasoningEffort.None, Output = ReasoningOutput.None },
                CopilotReasoningMode.Enabled => new ReasoningOptions { Effort = ReasoningEffort.Medium, Output = ReasoningOutput.Full },
                CopilotReasoningMode.High => new ReasoningOptions { Effort = ReasoningEffort.High, Output = ReasoningOutput.Full },
                CopilotReasoningMode.Max => new ReasoningOptions { Effort = ReasoningEffort.ExtraHigh, Output = ReasoningOutput.Full },
                _ => null,
            };
        }

        private static void ApplyCodexReasoningOptions(
            CopilotAgentRequest request,
            ChatOptions options)
        {
            var hasEffortOverride = request.CodexReasoningEffort !=
                CopilotCodexReasoningEffort.Unspecified;
            var hasSummaryOverride = request.CodexReasoningSummary !=
                CopilotCodexReasoningSummary.Unspecified;
            if ((!hasEffortOverride && !hasSummaryOverride)
                || !CopilotOpenAiRequestPolicy.UsesResponsesApi(request.Profile))
            {
                return;
            }

            options.Reasoning = null;
            options.RawRepresentationFactory = _ => new CreateResponseOptions
            {
                ReasoningOptions = BuildCodexResponseReasoningOptions(request),
            };
        }

        private static ResponseReasoningOptions? BuildCodexResponseReasoningOptions(
            CopilotAgentRequest request)
        {
            ResponseReasoningEffortLevel? effort = request.CodexReasoningEffort switch
            {
                CopilotCodexReasoningEffort.Minimal => ResponseReasoningEffortLevel.Minimal,
                CopilotCodexReasoningEffort.Low => ResponseReasoningEffortLevel.Low,
                CopilotCodexReasoningEffort.Medium => ResponseReasoningEffortLevel.Medium,
                CopilotCodexReasoningEffort.High => ResponseReasoningEffortLevel.High,
                CopilotCodexReasoningEffort.XHigh => new ResponseReasoningEffortLevel("xhigh"),
                _ => (ResponseReasoningEffortLevel?)null,
            };
            ResponseReasoningSummaryVerbosity? summary = request.CodexReasoningSummary switch
            {
                CopilotCodexReasoningSummary.Auto => ResponseReasoningSummaryVerbosity.Auto,
                CopilotCodexReasoningSummary.Concise => ResponseReasoningSummaryVerbosity.Concise,
                CopilotCodexReasoningSummary.Detailed => ResponseReasoningSummaryVerbosity.Detailed,
                _ => (ResponseReasoningSummaryVerbosity?)null,
            };
            return effort.HasValue || summary.HasValue
                ? new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = effort,
                    ReasoningSummaryVerbosity = summary,
                }
                : null;
        }

        private static Microsoft.Extensions.AI.ChatMessage ToFrameworkMessage(CopilotRequestMessage message)
        {
            var role = message.Role?.Trim().ToLowerInvariant() switch
            {
                "assistant" => ChatRole.Assistant,
                _ => ChatRole.User,
            };
            return new Microsoft.Extensions.AI.ChatMessage(role, message.Content ?? string.Empty);
        }

        private static CopilotTokenUsage ToCopilotUsage(UsageDetails details)
        {
            static int ToInt(long? value) => value.HasValue ? (int)Math.Clamp(value.Value, 0, int.MaxValue) : 0;

            return new CopilotTokenUsage(
                ToInt(details.InputTokenCount),
                ToInt(details.OutputTokenCount),
                ToInt(details.TotalTokenCount),
                details.CachedInputTokenCount.HasValue
                    ? ToInt(details.CachedInputTokenCount)
                    : null);
        }

        private static Action<CopilotAgentEvent> CreateEventEmitter(Action<CopilotAgentEvent> onEvent)
        {
            var syncRoot = new object();
            return agentEvent =>
            {
                lock (syncRoot)
                    onEvent(agentEvent);
            };
        }

        internal static bool ShouldResetAnswerBeforeEvent(CopilotAgentEventType eventType, int answerLength)
        {
            return answerLength > 0
                && eventType is CopilotAgentEventType.ToolStarted
                    or CopilotAgentEventType.ToolProgress
                    or CopilotAgentEventType.ToolResult
                    or CopilotAgentEventType.UserQuestionRequested;
        }

        internal static bool IsLengthLimitedOutput(AIChatFinishReason? finishReason)
        {
            return ClassifyOutputFinishReason(finishReason) == CopilotChatFinishKind.LengthLimit;
        }

        internal static bool IsContentFilteredOutput(AIChatFinishReason? finishReason)
        {
            return ClassifyOutputFinishReason(finishReason) == CopilotChatFinishKind.ContentFiltered;
        }

        internal static bool IsUnexpectedIncompleteOutput(AIChatFinishReason? finishReason)
        {
            return ClassifyOutputFinishReason(finishReason) is CopilotChatFinishKind.ToolRequested
                or CopilotChatFinishKind.Other;
        }

        private static CopilotChatFinishKind ClassifyOutputFinishReason(AIChatFinishReason? finishReason)
        {
            return finishReason.HasValue
                ? CopilotProviderFinishReasonClassifier.Classify(finishReason.Value.Value)
                : CopilotChatFinishKind.Unspecified;
        }
    }
}
