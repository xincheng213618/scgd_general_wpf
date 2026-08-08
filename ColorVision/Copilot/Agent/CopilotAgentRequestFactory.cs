using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentHostContextSnapshot
    {
        public string ActiveDocumentPath { get; }

        public string SolutionDirectoryPath { get; }

        public IReadOnlyList<CopilotAttachmentItem> Attachments { get; }

        public CopilotLiveContext? LiveContext { get; }

        public CopilotConversationHistorySnapshot ConversationHistory { get; }

        public IReadOnlyList<string> AdditionalReadRootPaths { get; }

        public CopilotAgentHostContextSnapshot(
            string? activeDocumentPath,
            string? solutionDirectoryPath,
            IEnumerable<CopilotAttachmentItem>? attachments,
            CopilotLiveContext? liveContext = null,
            CopilotConversationHistorySnapshot? conversationHistory = null,
            IEnumerable<string>? additionalReadRootPaths = null)
        {
            ActiveDocumentPath = activeDocumentPath ?? string.Empty;
            SolutionDirectoryPath = solutionDirectoryPath ?? string.Empty;
            LiveContext = CloneLiveContext(liveContext);
            Attachments = (attachments ?? Array.Empty<CopilotAttachmentItem>())
                .Where(attachment => attachment != null)
                .Select(attachment => CreateAttachmentSnapshot(attachment, LiveContext))
                .ToArray();
            ConversationHistory = conversationHistory == null
                ? CopilotConversationHistorySnapshot.Empty
                : new CopilotConversationHistorySnapshot(conversationHistory.ModelMessages, conversationHistory.VisibleMessages);
            AdditionalReadRootPaths = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(additionalReadRootPaths);
        }

        private static CopilotLiveContext? CloneLiveContext(CopilotLiveContext? source)
        {
            if (source == null)
                return null;

            return new CopilotLiveContext
            {
                SourceId = source.SourceId,
                Title = source.Title,
                Summary = source.Summary,
                AttachmentTitle = source.AttachmentTitle,
                SnapshotItems = (source.SnapshotItems ?? Array.Empty<CopilotContextItem>())
                    .Where(item => item != null)
                    .Select(item => new CopilotContextItem
                    {
                        Id = item.Id,
                        Title = item.Title,
                        Summary = item.Summary,
                        Content = item.Content,
                    })
                    .ToArray(),
            };
        }

        private static CopilotAttachmentItem CreateAttachmentSnapshot(
            CopilotAttachmentItem attachment,
            CopilotLiveContext? liveContext)
        {
            var snapshot = attachment.CreateSnapshot();
            if (liveContext == null
                || snapshot.Type != CopilotAttachmentType.Context
                || string.IsNullOrWhiteSpace(snapshot.Source)
                || !string.Equals(snapshot.Source, liveContext.SourceId, StringComparison.Ordinal)
                || liveContext.SnapshotItems.Count == 0)
            {
                return snapshot;
            }

            var latestContent = CopilotConversationRequestBuilder.BuildContextAttachmentContent(liveContext.SnapshotItems);
            if (string.IsNullOrWhiteSpace(latestContent))
                return snapshot;

            snapshot.Value = latestContent;
            if (!string.IsNullOrWhiteSpace(liveContext.AttachmentTitle))
                snapshot.Title = liveContext.AttachmentTitle.Trim();
            return snapshot;
        }
    }

    public sealed class CopilotAgentRequestPlan
    {
        public string UserText { get; init; } = string.Empty;

        public CopilotAgentMode Mode { get; init; } = CopilotAgentMode.Auto;

        public CopilotContextRequest ContextRequest { get; init; } = new();

        public IReadOnlyList<CopilotAttachmentItem> Attachments { get; init; } = Array.Empty<CopilotAttachmentItem>();

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> TrustedProjectRootPaths { get; init; } = Array.Empty<string>();

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public IReadOnlyList<CopilotProjectInstructionDocument> ProjectInstructions { get; init; } = Array.Empty<CopilotProjectInstructionDocument>();

        public IReadOnlyList<string> ReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ReadableLocalDirectoryPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableLocalRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableLocalFilePaths { get; init; } = Array.Empty<string>();

        public bool PreferBatchReadLocalFiles { get; init; }

        internal bool RequiresDelegatedWorkspaceEvidence { get; init; }
    }

    public sealed class CopilotAgentRequestBuildInput
    {
        public string ConversationId { get; init; } = string.Empty;

        public string TaskId { get; init; } = string.Empty;

        public string WorkspacePath { get; init; } = string.Empty;

        public CopilotProfileConfig Profile { get; init; } = null!;

        public IReadOnlyList<CopilotRequestMessage> History { get; init; } = Array.Empty<CopilotRequestMessage>();

        public IReadOnlyList<CopilotContextItem> ContextItems { get; init; } = Array.Empty<CopilotContextItem>();

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentRecoveryRequest? Recovery { get; init; }

        public CopilotAgentRunControl? RunControl { get; init; }

        public CopilotAgentDefaultsConfig AgentDefaults { get; init; } = new();

        public IReadOnlyList<CopilotMcpClientServerConfig> ExternalMcpServers { get; init; } = Array.Empty<CopilotMcpClientServerConfig>();

        public CopilotAgentAccessContext AccessContext { get; init; } = new();

        public string TaskIntentText { get; init; } = string.Empty;

        public string ActiveGoalText { get; init; } = string.Empty;

        public CopilotWorkspaceReviewTargetContext? WorkspaceReviewTarget { get; init; }
    }

    public static class CopilotAgentRequestFactory
    {
        public static CopilotAgentRequestPlan Prepare(
            string? userText,
            CopilotAgentMode mode,
            CopilotAgentHostContextSnapshot hostContext)
        {
            ArgumentNullException.ThrowIfNull(hostContext);

            var normalizedUserText = (userText ?? string.Empty).Trim();
            var explicitLocalPaths = CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(normalizedUserText);
            var explicitLocalDirectoryPaths = explicitLocalPaths
                .Where(IsExistingDirectoryPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var additionalReadRootPaths = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                hostContext.AdditionalReadRootPaths);
            var readableLocalDirectoryPaths = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(
                additionalReadRootPaths.Concat(explicitLocalDirectoryPaths));
            var explicitLocalFilePaths = explicitLocalPaths
                .Where(path => !IsExistingDirectoryPath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var searchRootPaths = BuildSearchRootPaths(hostContext, explicitLocalPaths);
            var trustedProjectRootPaths = BuildTrustedProjectRootPaths(hostContext);
            var workspaceWritableLocalRootPaths = CopilotWorkspaceSearchSupport.NormalizeSearchRoots([hostContext.SolutionDirectoryPath]);
            var requestedWritableLocalRootPaths = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(
                workspaceWritableLocalRootPaths.Concat(explicitLocalDirectoryPaths));
            var writableLocalFilePaths = BuildWritableLocalFilePaths(hostContext, explicitLocalFilePaths);
            var intentProbe = new CopilotAgentRequest
            {
                UserText = normalizedUserText,
                Mode = mode,
                ReadableLocalFilePaths = explicitLocalFilePaths,
                ReadableLocalDirectoryPaths = readableLocalDirectoryPaths,
                WritableLocalRootPaths = requestedWritableLocalRootPaths,
                WritableLocalFilePaths = writableLocalFilePaths,
            };
            var requiresWorkspaceEvidence = CopilotToolIntentPolicy.NeedsLocalEvidence(intentProbe);
            var requiresDelegatedWorkspaceEvidence =
                CopilotToolIntentPolicy.ExplicitlyRequiresDelegatedWorkspaceEvidence(intentProbe);
            var writableLocalRootPaths = CopilotToolIntentPolicy.NeedsWorkspaceCreate(intentProbe)
                || CopilotToolIntentPolicy.NeedsWorkspaceEdit(intentProbe)
                ? requestedWritableLocalRootPaths
                : workspaceWritableLocalRootPaths;
            var projectInstructions = mode == CopilotAgentMode.Chat
                || !requiresWorkspaceEvidence
                ? Array.Empty<CopilotProjectInstructionDocument>()
                : CopilotAgentProjectInstructions.Discover(
                    trustedProjectRootPaths,
                    hostContext.ActiveDocumentPath,
                    explicitLocalFilePaths.Concat(hostContext.Attachments
                        .Where(attachment => attachment.Type == CopilotAttachmentType.File)
                        .Select(attachment => attachment.Value)));

            return new CopilotAgentRequestPlan
            {
                UserText = normalizedUserText,
                Mode = mode,
                ContextRequest = new CopilotContextRequest
                {
                    Scope = MapContextScope(mode),
                    UserText = normalizedUserText,
                    SolutionDirectoryPath = hostContext.SolutionDirectoryPath,
                    ActiveDocumentPath = hostContext.ActiveDocumentPath,
                    SearchRootPaths = searchRootPaths,
                    RequiresWorkspaceEvidence = requiresWorkspaceEvidence,
                },
                Attachments = hostContext.Attachments,
                SearchRootPaths = searchRootPaths,
                TrustedProjectRootPaths = trustedProjectRootPaths,
                ActiveDocumentPath = hostContext.ActiveDocumentPath,
                ProjectInstructions = projectInstructions,
                ReadableLocalFilePaths = explicitLocalFilePaths,
                ReadableLocalDirectoryPaths = readableLocalDirectoryPaths,
                WritableLocalRootPaths = writableLocalRootPaths,
                WritableLocalFilePaths = writableLocalFilePaths,
                PreferBatchReadLocalFiles = explicitLocalDirectoryPaths.Length > 0 && explicitLocalFilePaths.Length == 0,
                RequiresDelegatedWorkspaceEvidence = requiresDelegatedWorkspaceEvidence,
            };
        }

        public static CopilotAgentRequest Create(CopilotAgentRequestPlan plan, CopilotAgentRequestBuildInput input)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(input.Profile);
            ArgumentNullException.ThrowIfNull(input.AgentDefaults);

            var agentDefaults = input.AgentDefaults.Clone();
            return new CopilotAgentRequest
            {
                ConversationId = (input.ConversationId ?? string.Empty).Trim(),
                TaskId = (input.TaskId ?? string.Empty).Trim(),
                WorkspacePath = (input.WorkspacePath ?? string.Empty).Trim(),
                UserText = plan.UserText,
                TaskIntentText = string.IsNullOrWhiteSpace(input.TaskIntentText)
                    ? plan.UserText
                    : input.TaskIntentText.Trim(),
                ActiveGoalText = CopilotConversationGoal.TryNormalizeObjective(
                    input.ActiveGoalText,
                    out var normalizedGoal,
                    out _)
                    ? normalizedGoal
                    : string.Empty,
                WorkspaceReviewTarget = plan.Mode == CopilotAgentMode.Review
                    && input.WorkspaceReviewTarget?.IsStructurallyValid() == true
                        ? input.WorkspaceReviewTarget.CreateSnapshot()
                        : null,
                Profile = input.Profile,
                History = input.History.ToArray(),
                Attachments = plan.Attachments,
                ContextItems = input.ContextItems.ToArray(),
                SearchRootPaths = plan.SearchRootPaths,
                TrustedProjectRootPaths = plan.TrustedProjectRootPaths,
                ActiveDocumentPath = plan.ActiveDocumentPath,
                ProjectInstructions = plan.ProjectInstructions,
                ReadableLocalFilePaths = plan.ReadableLocalFilePaths,
                ReadableLocalDirectoryPaths = plan.ReadableLocalDirectoryPaths,
                WritableLocalRootPaths = plan.WritableLocalRootPaths,
                WritableLocalFilePaths = plan.WritableLocalFilePaths,
                PreferBatchReadLocalFiles = plan.PreferBatchReadLocalFiles,
                PreferredShell = agentDefaults.PreferredShell,
                Mode = plan.Mode,
                SessionCheckpoint = input.SessionCheckpoint,
                Recovery = input.SessionCheckpoint == null ? null : input.Recovery,
                RunControl = input.RunControl,
                RunBudgetDefaults = agentDefaults.CreateRunBudgetDefaults(),
                SkillOverrides = agentDefaults.CreateSkillOverrideSnapshot(),
                AccessContext = input.AccessContext,
                ExternalMcpServers = input.ExternalMcpServers
                    .Where(server => server?.Enabled == true)
                    .Select(server => server.Clone())
                    .ToArray(),
                RequiredSuccessfulToolNames = plan.RequiresDelegatedWorkspaceEvidence
                    ? ["DelegateExplore"]
                    : Array.Empty<string>(),
                RequiresDelegatedWorkspaceEvidence = plan.RequiresDelegatedWorkspaceEvidence,
            };
        }

        public static IReadOnlyList<string> BuildSearchRootPaths(
            CopilotAgentHostContextSnapshot hostContext,
            IReadOnlyList<string> explicitLocalPaths)
        {
            ArgumentNullException.ThrowIfNull(hostContext);
            ArgumentNullException.ThrowIfNull(explicitLocalPaths);

            var roots = new List<string>();
            AddSearchCandidate(roots, hostContext.SolutionDirectoryPath);
            AddSearchCandidate(roots, hostContext.ActiveDocumentPath);

            foreach (var path in explicitLocalPaths)
                AddSearchCandidate(roots, path);

            foreach (var path in hostContext.AdditionalReadRootPaths)
                AddSearchCandidate(roots, path);

            foreach (var attachment in hostContext.Attachments.Where(item => item.Type == CopilotAttachmentType.File && !string.IsNullOrWhiteSpace(item.Value)))
                AddSearchCandidate(roots, attachment.Value);

            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots);
        }

        public static IReadOnlyList<string> BuildTrustedProjectRootPaths(CopilotAgentHostContextSnapshot hostContext)
        {
            ArgumentNullException.ThrowIfNull(hostContext);

            var roots = new List<string>();
            AddSearchCandidate(roots, hostContext.SolutionDirectoryPath);
            if (roots.Count == 0)
                AddSearchCandidate(roots, hostContext.ActiveDocumentPath);
            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots);
        }

        private static string[] BuildWritableLocalFilePaths(
            CopilotAgentHostContextSnapshot hostContext,
            IReadOnlyList<string> explicitLocalFilePaths)
        {
            return explicitLocalFilePaths
                .Append(hostContext.ActiveDocumentPath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Select(Path.GetFullPath)
                .Where(CopilotWorkspaceSearchSupport.IsTextLikeFile)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static CopilotContextScope MapContextScope(CopilotAgentMode mode)
        {
            return mode == CopilotAgentMode.Diagnose
                ? CopilotContextScope.Diagnose
                : mode == CopilotAgentMode.Chat
                    ? CopilotContextScope.Chat
                    : CopilotContextScope.Agent;
        }

        private static void AddSearchCandidate(List<string> roots, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    roots.Add(fullPath);
                    return;
                }

                if (File.Exists(fullPath))
                {
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        roots.Add(directory);
                    return;
                }

                // An explicitly named file may not exist yet (for example, a
                // requested source file that was renamed or generated later).
                // Keep its existing absolute parent searchable so the Agent can
                // inspect nearby candidates and report the missing path with
                // useful workspace evidence instead of losing the only scope.
                if (Path.IsPathFullyQualified(path))
                {
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                        roots.Add(directory);
                }
            }
            catch
            {
            }
        }

        private static bool IsExistingDirectoryPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                return Directory.Exists(Path.GetFullPath(path));
            }
            catch
            {
                return false;
            }
        }
    }
}
