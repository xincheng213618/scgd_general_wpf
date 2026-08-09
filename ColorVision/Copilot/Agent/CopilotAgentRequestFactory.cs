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

        internal string GlobalInstructionRootPath { get; }

        internal string PrimaryTrustedProjectRootPath { get; }

        internal string ProjectConfigWorkingDirectoryPath { get; }

        internal CopilotProjectInstructionDiscoveryOptions ProjectInstructionDiscoveryOptions { get; }

        public CopilotAgentHostContextSnapshot(
            string? activeDocumentPath,
            string? solutionDirectoryPath,
            IEnumerable<CopilotAttachmentItem>? attachments,
            CopilotLiveContext? liveContext = null,
            CopilotConversationHistorySnapshot? conversationHistory = null,
            IEnumerable<string>? additionalReadRootPaths = null)
            : this(
                activeDocumentPath,
                solutionDirectoryPath,
                attachments,
                liveContext,
                conversationHistory,
                additionalReadRootPaths,
                globalInstructionRootPath: null,
                loadCodexConfigLayers: false)
        {
        }

        internal CopilotAgentHostContextSnapshot(
            string? activeDocumentPath,
            string? solutionDirectoryPath,
            IEnumerable<CopilotAttachmentItem>? attachments,
            CopilotLiveContext? liveContext,
            CopilotConversationHistorySnapshot? conversationHistory,
            IEnumerable<string>? additionalReadRootPaths,
            string? globalInstructionRootPath)
            : this(
                activeDocumentPath,
                solutionDirectoryPath,
                attachments,
                liveContext,
                conversationHistory,
                additionalReadRootPaths,
                globalInstructionRootPath,
                loadCodexConfigLayers: true)
        {
        }

        private CopilotAgentHostContextSnapshot(
            string? activeDocumentPath,
            string? solutionDirectoryPath,
            IEnumerable<CopilotAttachmentItem>? attachments,
            CopilotLiveContext? liveContext,
            CopilotConversationHistorySnapshot? conversationHistory,
            IEnumerable<string>? additionalReadRootPaths,
            string? globalInstructionRootPath,
            bool loadCodexConfigLayers)
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
            GlobalInstructionRootPath = CopilotAgentProjectInstructions.NormalizeGlobalInstructionRootPath(globalInstructionRootPath);
            ProjectConfigWorkingDirectoryPath = CopilotAgentRequestFactory.ResolvePrimaryProjectWorkingDirectoryPath(
                SolutionDirectoryPath,
                ActiveDocumentPath);
            if (loadCodexConfigLayers)
            {
                var codexHome = CopilotProjectInstructionDiscoveryConfig.LoadCodexHome(GlobalInstructionRootPath);
                PrimaryTrustedProjectRootPath = CopilotAgentRequestFactory.ResolvePrimaryTrustedProjectRootPath(
                    SolutionDirectoryPath,
                    ActiveDocumentPath,
                    codexHome.Options.ProjectRootMarkers);
                ProjectInstructionDiscoveryOptions = CopilotProjectInstructionDiscoveryConfig.LoadTrustedProjectLayers(
                    codexHome,
                    PrimaryTrustedProjectRootPath,
                    ProjectConfigWorkingDirectoryPath);
            }
            else
            {
                ProjectInstructionDiscoveryOptions = CopilotProjectInstructionDiscoveryConfig.CreateDefault();
                PrimaryTrustedProjectRootPath = CopilotAgentRequestFactory.ResolvePrimaryTrustedProjectRootPath(
                    SolutionDirectoryPath,
                    ActiveDocumentPath,
                    ProjectInstructionDiscoveryOptions.ProjectRootMarkers);
            }
        }

        internal CopilotAgentHostContextSnapshot WithConversationHistory(
            CopilotConversationHistorySnapshot? conversationHistory)
        {
            return new CopilotAgentHostContextSnapshot(
                ActiveDocumentPath,
                SolutionDirectoryPath,
                Attachments,
                LiveContext,
                conversationHistory,
                AdditionalReadRootPaths,
                GlobalInstructionRootPath,
                PrimaryTrustedProjectRootPath,
                ProjectConfigWorkingDirectoryPath,
                ProjectInstructionDiscoveryOptions);
        }

        private CopilotAgentHostContextSnapshot(
            string activeDocumentPath,
            string solutionDirectoryPath,
            IEnumerable<CopilotAttachmentItem> attachments,
            CopilotLiveContext? liveContext,
            CopilotConversationHistorySnapshot? conversationHistory,
            IEnumerable<string> additionalReadRootPaths,
            string globalInstructionRootPath,
            string primaryTrustedProjectRootPath,
            string projectConfigWorkingDirectoryPath,
            CopilotProjectInstructionDiscoveryOptions projectInstructionDiscoveryOptions)
        {
            ActiveDocumentPath = activeDocumentPath;
            SolutionDirectoryPath = solutionDirectoryPath;
            LiveContext = CloneLiveContext(liveContext);
            Attachments = attachments.Select(attachment => attachment.CreateSnapshot()).ToArray();
            ConversationHistory = conversationHistory == null
                ? CopilotConversationHistorySnapshot.Empty
                : new CopilotConversationHistorySnapshot(
                    conversationHistory.ModelMessages,
                    conversationHistory.VisibleMessages);
            AdditionalReadRootPaths = additionalReadRootPaths.ToArray();
            GlobalInstructionRootPath = globalInstructionRootPath;
            PrimaryTrustedProjectRootPath = primaryTrustedProjectRootPath;
            ProjectConfigWorkingDirectoryPath = projectConfigWorkingDirectoryPath;
            ProjectInstructionDiscoveryOptions = projectInstructionDiscoveryOptions;
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

        public string ConfiguredDeveloperInstructions { get; init; } = string.Empty;

        internal CopilotCodexWebSearchMode CodexWebSearchMode { get; init; } =
            CopilotCodexWebSearchMode.Unspecified;

        internal CopilotCodexSandboxMode CodexSandboxMode { get; init; } =
            CopilotCodexSandboxMode.Unspecified;

        internal bool CodexShellToolEnabled { get; init; } = true;

        internal bool CodexHooksEnabled { get; init; } = true;

        internal IReadOnlyList<CopilotCodexCommandHookDefinition> CodexCommandHooks { get; init; } =
            Array.Empty<CopilotCodexCommandHookDefinition>();

        internal IReadOnlyList<CopilotCodexExecPolicyRule> CodexExecPolicyRules { get; init; } =
            Array.Empty<CopilotCodexExecPolicyRule>();

        internal bool CodexPluginsEnabled { get; init; } = true;

        internal bool CodexErrorOnToolCollisions { get; init; }

        internal CopilotCodexShellEnvironmentPolicy CodexShellEnvironmentPolicy { get; init; } =
            CopilotCodexShellEnvironmentPolicy.Default;

        internal bool CodexExperimentalRequestUserInputEnabled { get; init; } = true;

        internal bool CodexDefaultModeRequestUserInputEnabled { get; init; }

        internal bool CodexUpdatePlanEnabled { get; init; } = true;

        internal bool CodexIncludePermissionsInstructions { get; init; } = true;

        internal bool CodexIncludeCollaborationModeInstructions { get; init; } = true;

        internal bool CodexIncludeEnvironmentContext { get; init; } = true;

        internal bool CodexIncludeSkillInstructions { get; init; } = true;

        internal bool CodexGoalsEnabled { get; init; } = true;

        internal CopilotCodexApprovalPolicy CodexApprovalPolicy { get; init; } =
            CopilotCodexApprovalPolicy.Unspecified;

        internal CopilotCodexApprovalsReviewer CodexApprovalsReviewer { get; init; } =
            CopilotCodexApprovalsReviewer.Unspecified;

        internal bool CodexGuardianApprovalEnabled { get; init; } = true;

        internal string CodexAutoReviewPolicy { get; init; } = string.Empty;

        internal bool CodexAgentsEnabled { get; init; } = true;

        internal bool CodexInterruptMessageEnabled { get; init; } = true;

        internal int CodexMaximumConcurrentSubagentRuns { get; init; } =
            CopilotSubagentCoordinator.DefaultMaximumConcurrentRuns;

        internal string CodexDefaultSubagentModel { get; init; } = string.Empty;

        internal CopilotCodexReasoningEffort CodexDefaultSubagentReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        internal IReadOnlyList<CopilotCodexCustomSubagentDefinition> CodexCustomSubagents { get; init; } =
            Array.Empty<CopilotCodexCustomSubagentDefinition>();

        internal int? ModelContextWindowTokensOverride { get; init; }

        internal int? ToolOutputTokenLimitOverride { get; init; }

        internal CopilotCodexReasoningEffort CodexReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        internal CopilotCodexReasoningSummary CodexReasoningSummary { get; init; } =
            CopilotCodexReasoningSummary.Unspecified;

        internal bool? CodexModelSupportsReasoningSummaries { get; init; }

        internal bool CodexFastModeEnabled { get; init; } = true;

        internal string CodexServiceTier { get; init; } = string.Empty;

        internal CopilotCodexModelVerbosity CodexModelVerbosity { get; init; } =
            CopilotCodexModelVerbosity.Unspecified;

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

        internal IReadOnlyList<string> UserPromptSubmitAdditionalContexts { get; init; } =
            Array.Empty<string>();

        internal IReadOnlyList<string> SessionStartAdditionalContexts { get; init; } =
            Array.Empty<string>();

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentRecoveryRequest? Recovery { get; init; }

        public CopilotAgentRunControl? RunControl { get; init; }

        public CopilotAgentDefaultsConfig AgentDefaults { get; init; } = new();

        public IReadOnlyList<CopilotMcpClientServerConfig> ExternalMcpServers { get; init; } = Array.Empty<CopilotMcpClientServerConfig>();

        public CopilotAgentAccessContext AccessContext { get; init; } = new();

        public string TaskIntentText { get; init; } = string.Empty;

        public string ActiveGoalText { get; init; } = string.Empty;

        public CopilotWorkspaceReviewTargetContext? WorkspaceReviewTarget { get; init; }

        public CopilotAgentSkillReference? AgentSkillReference { get; init; }
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
            var codexSandboxMode = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredSandboxMode;
            var codexApprovalPolicy = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredApprovalPolicy;
            var codexGuardianApprovalEnabled = hostContext.ProjectInstructionDiscoveryOptions
                .ConfiguredGuardianApprovalEnabled;
            var codexApprovalsReviewer = hostContext.ProjectInstructionDiscoveryOptions.EffectiveApprovalsReviewer;
            var codexAutoReviewPolicy = codexGuardianApprovalEnabled
                ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredAutoReviewPolicy
                : string.Empty;
            var codexReadOnly = CopilotCodexSandboxModeSelection.IsReadOnly(codexSandboxMode);
            var workspaceWritableLocalRootPaths = codexReadOnly
                ? Array.Empty<string>()
                : CopilotWorkspaceSearchSupport.NormalizeSearchRoots([hostContext.SolutionDirectoryPath]);
            var requestedWritableLocalRootPaths = codexReadOnly
                ? Array.Empty<string>()
                : CopilotWorkspaceSearchSupport.NormalizeSearchRoots(
                    workspaceWritableLocalRootPaths.Concat(explicitLocalDirectoryPaths));
            var writableLocalFilePaths = codexReadOnly
                ? Array.Empty<string>()
                : BuildWritableLocalFilePaths(hostContext, explicitLocalFilePaths);
            var intentProbe = new CopilotAgentRequest
            {
                UserText = normalizedUserText,
                Mode = mode,
                CodexSandboxMode = codexSandboxMode,
                CodexApprovalPolicy = codexApprovalPolicy,
                CodexApprovalsReviewer = codexApprovalsReviewer,
                CodexGuardianApprovalEnabled = codexGuardianApprovalEnabled,
                CodexAutoReviewPolicy = codexAutoReviewPolicy,
                ReadableLocalFilePaths = explicitLocalFilePaths,
                ReadableLocalDirectoryPaths = readableLocalDirectoryPaths,
                WritableLocalRootPaths = requestedWritableLocalRootPaths,
                WritableLocalFilePaths = writableLocalFilePaths,
            };
            var requiresWorkspaceEvidence = CopilotToolIntentPolicy.NeedsLocalEvidence(intentProbe);
            var requiresDelegatedWorkspaceEvidence =
                hostContext.ProjectInstructionDiscoveryOptions.EffectiveAgentsEnabled
                && CopilotToolIntentPolicy.ExplicitlyRequiresDelegatedWorkspaceEvidence(intentProbe);
            var writableLocalRootPaths = CopilotToolIntentPolicy.NeedsWorkspaceCreate(intentProbe)
                || CopilotToolIntentPolicy.NeedsWorkspaceEdit(intentProbe)
                ? requestedWritableLocalRootPaths
                : workspaceWritableLocalRootPaths;
            var projectInstructions = mode == CopilotAgentMode.Chat
                || !requiresWorkspaceEvidence
                ? Array.Empty<CopilotProjectInstructionDocument>()
                : CopilotAgentProjectInstructions.DiscoverWithGlobal(
                    trustedProjectRootPaths,
                    hostContext.ActiveDocumentPath,
                    explicitLocalFilePaths.Concat(hostContext.Attachments
                        .Where(attachment => attachment.Type == CopilotAttachmentType.File)
                        .Select(attachment => attachment.Value)),
                    hostContext.GlobalInstructionRootPath,
                    hostContext.ProjectInstructionDiscoveryOptions);

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
                    IncludeExtensionProviders = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredPluginsEnabled,
                },
                Attachments = hostContext.Attachments,
                SearchRootPaths = searchRootPaths,
                TrustedProjectRootPaths = trustedProjectRootPaths,
                ActiveDocumentPath = hostContext.ActiveDocumentPath,
                ConfiguredDeveloperInstructions = hostContext.ProjectInstructionDiscoveryOptions.DeveloperInstructions,
                CodexWebSearchMode = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredWebSearchMode,
                CodexSandboxMode = codexSandboxMode,
                CodexShellToolEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredShellToolEnabled,
                CodexHooksEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredHooksEnabled,
                CodexCommandHooks = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredCommandHooks
                    .Select(definition => definition.CreateSnapshot())
                    .ToArray(),
                CodexExecPolicyRules = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredExecPolicyRules
                    .Select(rule => rule.CreateSnapshot())
                    .ToArray(),
                CodexPluginsEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredPluginsEnabled,
                CodexErrorOnToolCollisions = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredErrorOnToolCollisions,
                CodexShellEnvironmentPolicy = hostContext.ProjectInstructionDiscoveryOptions
                    .ConfiguredShellEnvironmentPolicy.CreateSnapshot(),
                CodexExperimentalRequestUserInputEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredExperimentalRequestUserInputEnabled,
                CodexDefaultModeRequestUserInputEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredDefaultModeRequestUserInputEnabled,
                CodexUpdatePlanEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredUpdatePlanEnabled,
                CodexIncludePermissionsInstructions = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredIncludePermissionsInstructions,
                CodexIncludeCollaborationModeInstructions = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredIncludeCollaborationModeInstructions,
                CodexIncludeEnvironmentContext = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredIncludeEnvironmentContext,
                CodexIncludeSkillInstructions = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredIncludeSkillInstructions,
                CodexGoalsEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredGoalsEnabled,
                CodexApprovalPolicy = codexApprovalPolicy,
                CodexApprovalsReviewer = codexApprovalsReviewer,
                CodexGuardianApprovalEnabled = codexGuardianApprovalEnabled,
                CodexAutoReviewPolicy = codexAutoReviewPolicy,
                CodexAgentsEnabled = hostContext.ProjectInstructionDiscoveryOptions.EffectiveAgentsEnabled,
                CodexInterruptMessageEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredInterruptMessageEnabled,
                CodexMaximumConcurrentSubagentRuns =
                    hostContext.ProjectInstructionDiscoveryOptions.ConfiguredMaximumConcurrentSubagentRuns,
                CodexDefaultSubagentModel = hostContext.ProjectInstructionDiscoveryOptions.HasDefaultSubagentModelOverride
                    ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredDefaultSubagentModel
                    : string.Empty,
                CodexDefaultSubagentReasoningEffort =
                    hostContext.ProjectInstructionDiscoveryOptions.HasDefaultSubagentReasoningEffortOverride
                        ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredDefaultSubagentReasoningEffort
                        : CopilotCodexReasoningEffort.Unspecified,
                CodexCustomSubagents = hostContext.ProjectInstructionDiscoveryOptions.CustomSubagents
                    .Select(definition => definition.CreateSnapshot())
                    .ToArray(),
                ModelContextWindowTokensOverride = hostContext.ProjectInstructionDiscoveryOptions.HasModelContextWindowOverride
                    ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredModelContextWindowTokens
                    : null,
                ToolOutputTokenLimitOverride = hostContext.ProjectInstructionDiscoveryOptions.HasToolOutputTokenLimitOverride
                    ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredToolOutputTokenLimit
                    : null,
                CodexReasoningEffort = mode == CopilotAgentMode.Plan
                    && hostContext.ProjectInstructionDiscoveryOptions.HasPlanModeReasoningEffortOverride
                        ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredPlanModeReasoningEffort
                        : hostContext.ProjectInstructionDiscoveryOptions.HasModelReasoningEffortOverride
                            ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredModelReasoningEffort
                            : CopilotCodexReasoningEffort.Unspecified,
                CodexReasoningSummary = hostContext.ProjectInstructionDiscoveryOptions.HasModelReasoningSummaryOverride
                    ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredModelReasoningSummary
                    : CopilotCodexReasoningSummary.Unspecified,
                CodexModelSupportsReasoningSummaries = hostContext.ProjectInstructionDiscoveryOptions.HasModelSupportsReasoningSummariesOverride
                    ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredModelSupportsReasoningSummaries
                    : null,
                CodexFastModeEnabled = hostContext.ProjectInstructionDiscoveryOptions.ConfiguredFastModeEnabled,
                CodexServiceTier = hostContext.ProjectInstructionDiscoveryOptions.HasServiceTierOverride
                    ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredServiceTier
                    : string.Empty,
                CodexModelVerbosity = hostContext.ProjectInstructionDiscoveryOptions.HasModelVerbosityOverride
                    ? hostContext.ProjectInstructionDiscoveryOptions.ConfiguredModelVerbosity
                    : CopilotCodexModelVerbosity.Unspecified,
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
            if (plan.ModelContextWindowTokensOverride.HasValue)
                agentDefaults.ContextWindowTokens = plan.ModelContextWindowTokensOverride.Value;
            return new CopilotAgentRequest
            {
                ConversationId = (input.ConversationId ?? string.Empty).Trim(),
                TaskId = (input.TaskId ?? string.Empty).Trim(),
                WorkspacePath = (input.WorkspacePath ?? string.Empty).Trim(),
                UserText = plan.UserText,
                TaskIntentText = string.IsNullOrWhiteSpace(input.TaskIntentText)
                    ? plan.UserText
                    : input.TaskIntentText.Trim(),
                ActiveGoalText = plan.CodexGoalsEnabled
                    && CopilotConversationGoal.TryNormalizeObjective(
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
                SessionStartAdditionalContexts = (input.SessionStartAdditionalContexts
                        ?? Array.Empty<string>())
                    .Where(context => !string.IsNullOrWhiteSpace(context))
                    .Select(context => context.Trim())
                    .ToArray(),
                UserPromptSubmitAdditionalContexts = (input.UserPromptSubmitAdditionalContexts
                        ?? Array.Empty<string>())
                    .Where(context => !string.IsNullOrWhiteSpace(context))
                    .Select(context => context.Trim())
                    .ToArray(),
                SearchRootPaths = plan.SearchRootPaths,
                TrustedProjectRootPaths = plan.TrustedProjectRootPaths,
                ActiveDocumentPath = plan.ActiveDocumentPath,
                ConfiguredDeveloperInstructions = plan.ConfiguredDeveloperInstructions,
                CodexWebSearchMode = plan.CodexWebSearchMode,
                CodexSandboxMode = plan.CodexSandboxMode,
                CodexShellToolEnabled = plan.CodexShellToolEnabled,
                CodexHooksEnabled = plan.CodexHooksEnabled,
                CodexCommandHooks = plan.CodexCommandHooks
                    .Where(definition => definition?.IsStructurallyValid() == true)
                    .Select(definition => definition.CreateSnapshot())
                    .ToArray(),
                CodexExecPolicyRules = plan.CodexExecPolicyRules
                    .Where(rule => rule?.IsStructurallyValid() == true)
                    .Select(rule => rule.CreateSnapshot())
                    .ToArray(),
                CodexPluginsEnabled = plan.CodexPluginsEnabled,
                CodexErrorOnToolCollisions = plan.CodexErrorOnToolCollisions,
                CodexShellEnvironmentPolicy = plan.CodexShellEnvironmentPolicy.CreateSnapshot(),
                CodexExperimentalRequestUserInputEnabled = plan.CodexExperimentalRequestUserInputEnabled,
                CodexDefaultModeRequestUserInputEnabled = plan.CodexDefaultModeRequestUserInputEnabled,
                CodexUpdatePlanEnabled = plan.CodexUpdatePlanEnabled,
                CodexIncludePermissionsInstructions = plan.CodexIncludePermissionsInstructions,
                CodexIncludeCollaborationModeInstructions = plan.CodexIncludeCollaborationModeInstructions,
                CodexIncludeEnvironmentContext = plan.CodexIncludeEnvironmentContext,
                CodexIncludeSkillInstructions = plan.CodexIncludeSkillInstructions,
                CodexApprovalPolicy = plan.CodexApprovalPolicy,
                CodexApprovalsReviewer = plan.CodexApprovalsReviewer,
                CodexGuardianApprovalEnabled = plan.CodexGuardianApprovalEnabled,
                CodexAutoReviewPolicy = plan.CodexAutoReviewPolicy,
                CodexAgentsEnabled = plan.CodexAgentsEnabled,
                CodexInterruptMessageEnabled = plan.CodexInterruptMessageEnabled,
                CodexMaximumConcurrentSubagentRuns = plan.CodexMaximumConcurrentSubagentRuns,
                CodexDefaultSubagentModel = plan.CodexDefaultSubagentModel,
                CodexDefaultSubagentReasoningEffort = plan.CodexDefaultSubagentReasoningEffort,
                CodexCustomSubagents = plan.CodexCustomSubagents
                    .Select(definition => definition.CreateSnapshot())
                    .ToArray(),
                ToolOutputTokenLimitOverride = plan.ToolOutputTokenLimitOverride,
                CodexReasoningEffort = plan.CodexReasoningEffort,
                CodexReasoningSummary = plan.CodexReasoningSummary,
                CodexModelSupportsReasoningSummaries = plan.CodexModelSupportsReasoningSummaries,
                CodexFastModeEnabled = plan.CodexFastModeEnabled,
                CodexServiceTier = plan.CodexFastModeEnabled
                    ? plan.CodexServiceTier
                    : string.Empty,
                CodexModelVerbosity = plan.CodexModelVerbosity,
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
                SkillPathOverrides = agentDefaults.CreateSkillPathOverrideSnapshot(),
                AgentSkillReference = input.AgentSkillReference?.IsStructurallyValid() == true
                    && input.AgentSkillReference.IsExplicitlyInvokedBy(plan.UserText)
                        ? input.AgentSkillReference.CreateSnapshot()
                        : null,
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

            var root = hostContext.PrimaryTrustedProjectRootPath;
            return root.Length == 0 ? Array.Empty<string>() : [root];
        }

        internal static string ResolvePrimaryTrustedProjectRootPath(
            string? solutionDirectoryPath,
            string? activeDocumentPath)
            => ResolvePrimaryTrustedProjectRootPath(
                solutionDirectoryPath,
                activeDocumentPath,
                CopilotProjectInstructionDiscoveryConfig.DefaultProjectRootMarkers);

        internal static string ResolvePrimaryTrustedProjectRootPath(
            string? solutionDirectoryPath,
            string? activeDocumentPath,
            IEnumerable<string>? projectRootMarkers)
        {
            var workingDirectory = ResolvePrimaryProjectWorkingDirectoryPath(
                solutionDirectoryPath,
                activeDocumentPath);
            if (workingDirectory.Length == 0)
                return string.Empty;

            if (CopilotWorkspaceSearchSupport.HasReparsePointInPath(workingDirectory))
                return workingDirectory;

            var normalizedMarkers = (projectRootMarkers ?? Array.Empty<string>())
                .Select(CopilotProjectInstructionDiscoveryConfig.NormalizeProjectRootMarker)
                .Where(marker => marker.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(CopilotProjectInstructionDiscoveryConfig.MaximumProjectRootMarkers)
                .ToArray();
            if (normalizedMarkers.Length == 0)
                return workingDirectory;

            try
            {
                var current = new DirectoryInfo(workingDirectory);
                while (current != null)
                {
                    foreach (var marker in normalizedMarkers)
                    {
                        var markerPath = Path.Combine(current.FullName, marker);
                        if (Directory.Exists(markerPath) || File.Exists(markerPath))
                            return Path.TrimEndingDirectorySeparator(current.FullName);
                    }
                    current = current.Parent;
                }
            }
            catch
            {
            }

            return workingDirectory;
        }

        internal static string ResolvePrimaryProjectWorkingDirectoryPath(
            string? solutionDirectoryPath,
            string? activeDocumentPath)
        {
            var roots = new List<string>();
            AddSearchCandidate(roots, solutionDirectoryPath);
            if (roots.Count == 0)
                AddSearchCandidate(roots, activeDocumentPath);
            var normalizedRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots);
            return normalizedRoots.Count == 0 ? string.Empty : normalizedRoots[0];
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
