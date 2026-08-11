using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotComposerCaptureToken(
        string ConversationId,
        long Version);

    internal sealed record CopilotComposerCaptureSnapshot(
        CopilotComposerCaptureToken Token,
        string Text,
        CopilotAgentMode RequestMode,
        CopilotWorkspaceReviewTargetContext? WorkspaceReviewTarget,
        CopilotAgentSkillReference? AgentSkillReference)
    {
        public string ConversationId => Token.ConversationId;

        public long Version => Token.Version;
    }

    internal sealed class CopilotComposerSession
    {
        private string _conversationId = string.Empty;
        private CopilotWorkspaceReviewTargetContext? _workspaceReviewTarget;
        private CopilotAgentSkillReference? _agentSkillReference;

        public string Text { get; private set; } = string.Empty;

        public CopilotAgentMode RequestMode { get; private set; } = CopilotAgentMode.Auto;

        public CopilotWorkspaceReviewTargetContext? WorkspaceReviewTarget =>
            _workspaceReviewTarget?.CreateSnapshot();

        public CopilotAgentSkillReference? AgentSkillReference =>
            _agentSkillReference?.CreateSnapshot();

        public long Version { get; private set; }

        public void Load(CopilotConversationRecord? conversation)
        {
            var text = conversation?.DraftText ?? string.Empty;
            var requestMode = NormalizeRequestMode(
                conversation?.DraftRequestMode ?? CopilotAgentMode.Auto);

            _conversationId = conversation?.Id ?? string.Empty;
            Text = text;
            RequestMode = requestMode;
            _workspaceReviewTarget = NormalizeWorkspaceReviewTarget(
                conversation?.DraftWorkspaceReviewTarget,
                requestMode);
            _agentSkillReference = NormalizeAgentSkillReference(
                conversation?.DraftAgentSkillReference,
                text);
            AdvanceVersion();
        }

        public bool SetText(string? text)
        {
            var normalizedText = text ?? string.Empty;
            var normalizedSkillReference = NormalizeAgentSkillReference(
                _agentSkillReference,
                normalizedText);
            if (string.Equals(Text, normalizedText, StringComparison.Ordinal)
                && SkillReferencesEqual(_agentSkillReference, normalizedSkillReference))
            {
                return false;
            }

            Text = normalizedText;
            _agentSkillReference = normalizedSkillReference;
            AdvanceVersion();
            return true;
        }

        public bool SetRequestMode(CopilotAgentMode requestMode)
        {
            var normalizedMode = NormalizeRequestMode(requestMode);
            var normalizedReviewTarget = NormalizeWorkspaceReviewTarget(
                _workspaceReviewTarget,
                normalizedMode);
            if (RequestMode == normalizedMode
                && ReviewTargetsEqual(_workspaceReviewTarget, normalizedReviewTarget))
            {
                return false;
            }

            RequestMode = normalizedMode;
            _workspaceReviewTarget = normalizedReviewTarget;
            AdvanceVersion();
            return true;
        }

        public bool SetWorkspaceReviewTarget(CopilotWorkspaceReviewTargetContext? target)
        {
            var normalized = NormalizeWorkspaceReviewTarget(target, RequestMode);
            if (ReviewTargetsEqual(_workspaceReviewTarget, normalized))
                return false;

            _workspaceReviewTarget = normalized;
            AdvanceVersion();
            return true;
        }

        public bool SetAgentSkillReference(CopilotAgentSkillReference? reference)
        {
            var normalized = NormalizeAgentSkillReference(reference, Text);
            if (SkillReferencesEqual(_agentSkillReference, normalized))
                return false;

            _agentSkillReference = normalized;
            AdvanceVersion();
            return true;
        }

        public CopilotComposerCaptureSnapshot Capture()
        {
            var token = new CopilotComposerCaptureToken(_conversationId, Version);
            return new CopilotComposerCaptureSnapshot(
                token,
                Text,
                RequestMode,
                _workspaceReviewTarget?.CreateSnapshot(),
                _agentSkillReference?.CreateSnapshot());
        }

        public bool CommitScheduled(CopilotComposerCaptureToken token)
        {
            if (!string.Equals(token.ConversationId, _conversationId, StringComparison.Ordinal)
                || token.Version != Version)
            {
                return false;
            }

            Text = string.Empty;
            RequestMode = CopilotAgentMode.Auto;
            _workspaceReviewTarget = null;
            _agentSkillReference = null;
            AdvanceVersion();
            return true;
        }

        private void AdvanceVersion() => Version++;

        private static CopilotAgentMode NormalizeRequestMode(CopilotAgentMode requestMode) =>
            Enum.IsDefined(requestMode) ? requestMode : CopilotAgentMode.Auto;

        private static CopilotWorkspaceReviewTargetContext? NormalizeWorkspaceReviewTarget(
            CopilotWorkspaceReviewTargetContext? target,
            CopilotAgentMode requestMode) =>
            requestMode == CopilotAgentMode.Review
                && target?.IsStructurallyValid() == true
                    ? target.CreateSnapshot()
                    : null;

        private static CopilotAgentSkillReference? NormalizeAgentSkillReference(
            CopilotAgentSkillReference? reference,
            string text) =>
            reference?.IsStructurallyValid() == true
                && reference.IsExplicitlyInvokedBy(text)
                    ? reference.CreateSnapshot()
                    : null;

        private static bool ReviewTargetsEqual(
            CopilotWorkspaceReviewTargetContext? left,
            CopilotWorkspaceReviewTargetContext? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            return left.Target == right.Target
                && string.Equals(left.Revision, right.Revision, StringComparison.Ordinal);
        }

        private static bool SkillReferencesEqual(
            CopilotAgentSkillReference? left,
            CopilotAgentSkillReference? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            return string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    left.SkillFilePath,
                    right.SkillFilePath,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
