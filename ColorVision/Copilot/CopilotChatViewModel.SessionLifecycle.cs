using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private async Task<IReadOnlyList<string>> EndConversationSessionAsync(
            CopilotConversationRecord conversation)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            var diagnostics = new List<string>();
            CopilotAgentRequest request;
            try
            {
                request = CreateSessionEndHookRequest(conversation);
            }
            catch (Exception exception)
            {
                var message = "SessionEnd hook configuration failed to load; the session still closed.";
                diagnostics.Add(message);
                System.Diagnostics.Trace.TraceError(
                    $"{message} {CopilotAgentTraceEntry.Sanitize(exception.Message)}");
                request = CreateFallbackSessionEndHookRequest(conversation);
            }

            try
            {
                await _turnRuntime.RunSessionEndHooksAsync(
                    request,
                    diagnostics.Add,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                var message = "SessionEnd hook lifecycle failed open while the session was closing.";
                diagnostics.Add(message);
                System.Diagnostics.Trace.TraceError(
                    $"{message} {CopilotAgentTraceEntry.Sanitize(exception.Message)}");
            }
            return diagnostics.ToArray();
        }

        private static CopilotAgentRequest CreateFallbackSessionEndHookRequest(
            CopilotConversationRecord conversation) => new()
        {
            ConversationId = conversation.Id,
            TaskId = "session-end-" + Guid.NewGuid().ToString("N"),
            WorkspacePath = Environment.CurrentDirectory,
            Profile = new CopilotProfileConfig(),
            Mode = CopilotAgentMode.Chat,
            CodexHooksEnabled = false,
        };

        private async Task EndOpenSessionsForShutdownAsync()
        {
            var conversations = Conversations
                .Where(conversation => conversation?.IsArchived == false)
                .ToArray();
            await Task.WhenAll(conversations.Select(async conversation =>
            {
                var diagnostics = await EndConversationSessionAsync(conversation)
                    .ConfigureAwait(false);
                foreach (var diagnostic in diagnostics)
                {
                    System.Diagnostics.Trace.TraceInformation(
                        CopilotAgentTraceEntry.Sanitize(diagnostic));
                }
            })).ConfigureAwait(false);
        }

        private CopilotAgentRequest CreateSessionEndHookRequest(
            CopilotConversationRecord conversation)
        {
            var snapshot = CaptureHostedTurnSnapshot(conversation);
            var options = snapshot.ProjectInstructionDiscoveryOptions;
            var workspacePath = string.IsNullOrWhiteSpace(snapshot.SolutionDirectoryPath)
                ? snapshot.ProjectConfigWorkingDirectoryPath
                : snapshot.SolutionDirectoryPath;
            var profile = ResolveProfile(conversation.ProfileId)
                ?? ResolveProfile(_state.ActiveProfileId)
                ?? _config.GetPreferredDefaultProfile()
                ?? new CopilotProfileConfig();
            return new CopilotAgentRequest
            {
                ConversationId = conversation.Id,
                TaskId = "session-end-" + Guid.NewGuid().ToString("N"),
                WorkspacePath = workspacePath,
                Profile = profile.Clone(),
                Mode = CopilotAgentMode.Chat,
                SearchRootPaths = snapshot.AdditionalReadRootPaths,
                TrustedProjectRootPaths = string.IsNullOrWhiteSpace(
                    snapshot.PrimaryTrustedProjectRootPath)
                    ? Array.Empty<string>()
                    : [snapshot.PrimaryTrustedProjectRootPath],
                CodexHooksEnabled = options.ConfiguredHooksEnabled,
                CodexCommandHooks = options.ConfiguredCommandHooks
                    .Where(definition => definition?.IsStructurallyValid() == true)
                    .Select(definition => definition.CreateSnapshot())
                    .ToArray(),
                CodexShellEnvironmentPolicy = options
                    .ConfiguredShellEnvironmentPolicy.CreateSnapshot(),
                CodexApprovalPolicy = options.ConfiguredApprovalPolicy,
            };
        }

        private static string FormatSessionEndHookDiagnostics(
            IReadOnlyList<string> diagnostics)
        {
            var failures = (diagnostics ?? Array.Empty<string>())
                .Where(diagnostic => diagnostic.Contains(
                    "failed",
                    StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToArray();
            return failures.Length == 0
                ? string.Empty
                : "\n\nSessionEnd Hook：" + string.Join("\n", failures);
        }
    }
}
