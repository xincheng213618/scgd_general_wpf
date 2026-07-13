using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentRuntimeRouter : ICopilotAgentRuntime, ICopilotAgentSteeringRuntime
    {
        private readonly ICopilotAgentRuntime _builtInRuntime;
        private readonly ICopilotAgentRuntime _agentFrameworkRuntime;

        public CopilotAgentRuntimeRouter(ICopilotAgentRuntime builtInRuntime, ICopilotAgentRuntime agentFrameworkRuntime)
        {
            _builtInRuntime = builtInRuntime ?? throw new ArgumentNullException(nameof(builtInRuntime));
            _agentFrameworkRuntime = agentFrameworkRuntime ?? throw new ArgumentNullException(nameof(agentFrameworkRuntime));
        }

        public async Task<CopilotAgentRunResult> RunAsync(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onEvent);

            if (!CanUseAgentFramework(request.Profile, out var reason))
            {
                onEvent(CopilotAgentEvent.Status($"Agent Framework is unavailable for this profile ({reason}); using the built-in Agent runtime."));
                return await _builtInRuntime.RunAsync(request, onEvent, cancellationToken);
            }

            var hasMaterialProgress = false;
            try
            {
                return await _agentFrameworkRuntime.RunAsync(
                    request,
                    agentEvent =>
                    {
                        if (agentEvent.Type is CopilotAgentEventType.ToolStarted or CopilotAgentEventType.ToolResult or CopilotAgentEventType.AnswerDelta or CopilotAgentEventType.Completed)
                            hasMaterialProgress = true;
                        onEvent(agentEvent);
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!hasMaterialProgress)
            {
                onEvent(CopilotAgentEvent.Status($"Agent Framework failed before executing a tool or producing an answer ({ex.Message}); using the built-in Agent runtime."));
                return await _builtInRuntime.RunAsync(request, onEvent, cancellationToken);
            }
        }

        public bool TryEnqueueSteeringMessage(string message)
        {
            return _agentFrameworkRuntime is ICopilotAgentSteeringRuntime steeringRuntime
                && steeringRuntime.TryEnqueueSteeringMessage(message);
        }

        public static bool CanUseAgentFramework(CopilotProfileConfig? profile, out string reason)
        {
            if (profile == null || !profile.IsConfigured)
            {
                reason = "profile configuration is incomplete";
                return false;
            }

            if (profile.ProviderType is not (CopilotProviderType.OpenAICompatible or CopilotProviderType.AnthropicCompatible))
            {
                reason = "provider protocol is unsupported";
                return false;
            }

            if (!Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                reason = "base URL is invalid";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
