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

        public CopilotAgentHostContextSnapshot(
            string? activeDocumentPath,
            string? solutionDirectoryPath,
            IEnumerable<CopilotAttachmentItem>? attachments,
            CopilotLiveContext? liveContext = null,
            CopilotConversationHistorySnapshot? conversationHistory = null)
        {
            ActiveDocumentPath = activeDocumentPath ?? string.Empty;
            SolutionDirectoryPath = solutionDirectoryPath ?? string.Empty;
            Attachments = (attachments ?? Array.Empty<CopilotAttachmentItem>())
                .Where(attachment => attachment != null)
                .Select(CloneAttachment)
                .ToArray();
            LiveContext = CloneLiveContext(liveContext);
            ConversationHistory = conversationHistory == null
                ? CopilotConversationHistorySnapshot.Empty
                : new CopilotConversationHistorySnapshot(conversationHistory.ModelMessages, conversationHistory.VisibleMessages);
        }

        private static CopilotAttachmentItem CloneAttachment(CopilotAttachmentItem source)
        {
            return new CopilotAttachmentItem
            {
                Id = source.Id,
                Type = source.Type,
                Title = source.Title,
                Value = source.Value,
                Source = source.Source,
                CreatedAt = source.CreatedAt,
            };
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
    }

    public sealed class CopilotAgentRequestPlan
    {
        public string UserText { get; init; } = string.Empty;

        public CopilotAgentMode Mode { get; init; } = CopilotAgentMode.Auto;

        public CopilotContextRequest ContextRequest { get; init; } = new();

        public IReadOnlyList<CopilotAttachmentItem> Attachments { get; init; } = Array.Empty<CopilotAttachmentItem>();

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public IReadOnlyList<CopilotProjectInstructionDocument> ProjectInstructions { get; init; } = Array.Empty<CopilotProjectInstructionDocument>();

        public IReadOnlyList<string> ReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ReadableLocalDirectoryPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableLocalRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableLocalFilePaths { get; init; } = Array.Empty<string>();

        public bool PreferBatchReadLocalFiles { get; init; }
    }

    public sealed class CopilotAgentRequestBuildInput
    {
        public CopilotProfileConfig Profile { get; init; } = null!;

        public IReadOnlyList<CopilotRequestMessage> History { get; init; } = Array.Empty<CopilotRequestMessage>();

        public IReadOnlyList<CopilotContextItem> ContextItems { get; init; } = Array.Empty<CopilotContextItem>();

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentRecoveryRequest? Recovery { get; init; }

        public CopilotAgentRunControl? RunControl { get; init; }

        public CopilotAgentDefaultsConfig AgentDefaults { get; init; } = new();

        public IReadOnlyList<CopilotMcpClientServerConfig> ExternalMcpServers { get; init; } = Array.Empty<CopilotMcpClientServerConfig>();
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
            var explicitLocalFilePaths = explicitLocalPaths
                .Where(path => !IsExistingDirectoryPath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var searchRootPaths = BuildSearchRootPaths(hostContext, explicitLocalPaths);
            var writableLocalRootPaths = CopilotWorkspaceSearchSupport.NormalizeSearchRoots([hostContext.SolutionDirectoryPath]);
            var writableLocalFilePaths = BuildWritableLocalFilePaths(hostContext, explicitLocalFilePaths);
            var projectInstructions = mode == CopilotAgentMode.Chat
                ? Array.Empty<CopilotProjectInstructionDocument>()
                : CopilotAgentProjectInstructions.Discover(searchRootPaths, hostContext.ActiveDocumentPath);

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
                },
                Attachments = hostContext.Attachments,
                SearchRootPaths = searchRootPaths,
                ActiveDocumentPath = hostContext.ActiveDocumentPath,
                ProjectInstructions = projectInstructions,
                ReadableLocalFilePaths = explicitLocalFilePaths,
                ReadableLocalDirectoryPaths = explicitLocalDirectoryPaths,
                WritableLocalRootPaths = writableLocalRootPaths,
                WritableLocalFilePaths = writableLocalFilePaths,
                PreferBatchReadLocalFiles = explicitLocalDirectoryPaths.Length > 0 && explicitLocalFilePaths.Length == 0,
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
                UserText = plan.UserText,
                Profile = input.Profile,
                History = input.History.ToArray(),
                Attachments = plan.Attachments,
                ContextItems = input.ContextItems.ToArray(),
                SearchRootPaths = plan.SearchRootPaths,
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
                ExternalMcpServers = input.ExternalMcpServers
                    .Where(server => server?.Enabled == true)
                    .Select(server => server.Clone())
                    .ToArray(),
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

            foreach (var attachment in hostContext.Attachments.Where(item => item.Type == CopilotAttachmentType.File && !string.IsNullOrWhiteSpace(item.Value)))
                AddSearchCandidate(roots, attachment.Value);

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
