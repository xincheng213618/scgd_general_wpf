using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentRuntimeRouter : ICopilotAgentRuntime
    {
        private readonly ICopilotAgentRuntime _builtInRuntime;
        private readonly ICopilotAgentRuntime _agentFrameworkRuntime;

        public CopilotAgentRuntimeRouter(ICopilotAgentRuntime builtInRuntime, ICopilotAgentRuntime agentFrameworkRuntime)
        {
            _builtInRuntime = builtInRuntime ?? throw new ArgumentNullException(nameof(builtInRuntime));
            _agentFrameworkRuntime = agentFrameworkRuntime ?? throw new ArgumentNullException(nameof(agentFrameworkRuntime));
        }

        public Task<CopilotAgentRunResult> RunAsync(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onEvent);

            if (!request.Profile.UseAgentFramework)
                return _builtInRuntime.RunAsync(request, onEvent, cancellationToken);

            if (!CanUseAgentFramework(request.Profile, out var reason))
            {
                onEvent(CopilotAgentEvent.Status($"Agent Framework is unavailable for this profile ({reason}); using the built-in Agent runtime."));
                return _builtInRuntime.RunAsync(request, onEvent, cancellationToken);
            }

            return _agentFrameworkRuntime.RunAsync(request, onEvent, cancellationToken);
        }

        public static bool CanUseAgentFramework(CopilotProfileConfig? profile, out string reason)
        {
            if (profile == null || !profile.IsConfigured)
            {
                reason = "profile configuration is incomplete";
                return false;
            }

            if (profile.ProviderType != CopilotProviderType.OpenAICompatible)
            {
                reason = "only OpenAI-compatible profiles are enabled in the experiment";
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
