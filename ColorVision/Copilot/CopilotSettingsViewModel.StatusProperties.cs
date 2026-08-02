#pragma warning disable CA1822
namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        public string SettingsStatusText
        {
            get => _settingsStatusText;
            private set => SetProperty(ref _settingsStatusText, value ?? string.Empty);
        }
        private string _settingsStatusText = "Ready. Add a model or edit a profile, then Apply or Save.";

        public string McpDiagnosticsSummaryText
        {
            get => _mcpDiagnosticsSummaryText;
            private set => SetProperty(ref _mcpDiagnosticsSummaryText, value ?? string.Empty);
        }
        private string _mcpDiagnosticsSummaryText = string.Empty;

        public string McpServiceSummaryText
        {
            get => _mcpServiceSummaryText;
            private set => SetProperty(ref _mcpServiceSummaryText, value ?? string.Empty);
        }
        private string _mcpServiceSummaryText = string.Empty;

        public string McpActivitySummaryText
        {
            get => _mcpActivitySummaryText;
            private set => SetProperty(ref _mcpActivitySummaryText, value ?? string.Empty);
        }
        private string _mcpActivitySummaryText = string.Empty;

        public string McpPendingSummaryText
        {
            get => _mcpPendingSummaryText;
            private set => SetProperty(ref _mcpPendingSummaryText, value ?? string.Empty);
        }
        private string _mcpPendingSummaryText = string.Empty;

        public string McpErrorSummaryText
        {
            get => _mcpErrorSummaryText;
            private set => SetProperty(ref _mcpErrorSummaryText, value ?? string.Empty);
        }
        private string _mcpErrorSummaryText = string.Empty;

        public string McpDiagnosticsHeaderText
        {
            get => _mcpDiagnosticsHeaderText;
            private set => SetProperty(ref _mcpDiagnosticsHeaderText, value ?? string.Empty);
        }
        private string _mcpDiagnosticsHeaderText = "Diagnostics";

        public string McpLastErrorText
        {
            get => _mcpLastErrorText;
            private set => SetProperty(ref _mcpLastErrorText, value ?? string.Empty);
        }
        private string _mcpLastErrorText = string.Empty;

        public string McpRecentAuditText
        {
            get => _mcpRecentAuditText;
            private set => SetProperty(ref _mcpRecentAuditText, value ?? string.Empty);
        }
        private string _mcpRecentAuditText = string.Empty;

        public string SubagentRolesSummaryText
        {
            get => _subagentRolesSummaryText;
            private set => SetProperty(ref _subagentRolesSummaryText, value ?? string.Empty);
        }
        private string _subagentRolesSummaryText = string.Empty;

        public string SubagentRolesDiagnosticsText
        {
            get => _subagentRolesDiagnosticsText;
            private set => SetProperty(ref _subagentRolesDiagnosticsText, value ?? string.Empty);
        }
        private string _subagentRolesDiagnosticsText = string.Empty;

        public string AgentSkillsSummaryText
        {
            get => _agentSkillsSummaryText;
            private set => SetProperty(ref _agentSkillsSummaryText, value ?? string.Empty);
        }
        private string _agentSkillsSummaryText = string.Empty;

        public string AgentSkillsDiagnosticsText
        {
            get => _agentSkillsDiagnosticsText;
            private set => SetProperty(ref _agentSkillsDiagnosticsText, value ?? string.Empty);
        }
        private string _agentSkillsDiagnosticsText = string.Empty;
    }
}
