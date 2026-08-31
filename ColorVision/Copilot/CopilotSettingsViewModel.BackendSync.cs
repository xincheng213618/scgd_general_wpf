using ColorVision.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        internal async Task SyncBackendConfigAsync()
        {
            if (!CanSyncBackendConfig)
                return;

            IsSyncingBackendConfig = true;
            BackendSyncStatusText = "Downloading backend Copilot configuration...";
            SetSettingsNotice("Downloading backend Copilot configuration...");
            var profilesPersisted = false;
            var persistedRevision = "unknown";
            try
            {
                var syncBaseUrl = BackendSyncUrl.Trim();
                var response = await _backendSyncClient.FetchAsync(
                    syncBaseUrl,
                    _lifetimeCancellation.Token);
                // Closing the window can cancel after FetchAsync has already completed,
                // while this UI continuation is still waiting to run.
                if (_disposed)
                    return;

                var previousSelectedId = SelectedProfile?.Id ?? string.Empty;
                var config = _config;
                var syncPlan = CopilotBackendSyncTransaction.Prepare(
                    config,
                    Profiles,
                    response,
                    syncBaseUrl,
                    previousSelectedId,
                    _activeProfileId);
                var persistenceStatus = CopilotBackendSyncTransaction.TryPersist(
                        _configHandler,
                        config,
                        syncPlan,
                        out var persistenceError);
                if (persistenceStatus == ConfigSavePublicationStatus.NotPersisted)
                {
                    throw new InvalidOperationException(
                        "The downloaded profiles were not saved. " + persistenceError);
                }
                profilesPersisted = true;
                persistedRevision = string.IsNullOrWhiteSpace(response.Revision) ? "unknown" : response.Revision;
                HasAppliedChanges = true;
                if (persistenceStatus == ConfigSavePublicationStatus.PersistedButPublishFailed)
                {
                    throw new InvalidOperationException(
                        "The profiles were saved to disk but could not be published to the running application. "
                        + persistenceError);
                }

                _isApplyingPreset = true;
                _isSavingSettings = true;
                try
                {
                    Profiles.Clear();
                    foreach (var profile in syncPlan.DisplayProfiles.Select(profile => profile.Clone()))
                        Profiles.Add(profile);

                    SelectedProfile = Profiles.FirstOrDefault(profile =>
                            string.Equals(profile.Id, syncPlan.SelectedProfileId, StringComparison.Ordinal))
                        ?? Profiles.FirstOrDefault();
                    SetActiveProfileId(syncPlan.ActiveProfileId);
                }
                finally
                {
                    _isSavingSettings = false;
                    _isApplyingPreset = false;
                }

                HasUnsavedSettings = HasDraftSettingsDifferentFromConfig();
                RefreshSelectedProfileTestState("This profile was synchronized from the backend.");
                var mergeResult = syncPlan.MergeResult;
                BackendSyncStatusText =
                    $"Revision {persistedRevision}: {mergeResult.Added} added, {mergeResult.Updated} updated, {mergeResult.Removed} removed and saved.";
                SetSettingsNotice(HasUnsavedSettings
                    ? BackendSyncStatusText + " Other settings still have unsaved changes."
                    : BackendSyncStatusText);
            }
            catch (OperationCanceledException) when (_disposed)
            {
            }
            catch (Exception ex)
            {
                BackendSyncStatusText = profilesPersisted
                    ? $"Revision {persistedRevision} was saved, but the settings view could not refresh: {SanitizeError(ex.Message)}"
                    : "Sync failed: " + SanitizeError(ex.Message);
                SetSettingsNotice(BackendSyncStatusText);
            }
            finally
            {
                IsSyncingBackendConfig = false;
            }
        }

        private bool HasDraftSettingsDifferentFromConfig()
        {
            var config = _config;
            try
            {
                if (!JToken.DeepEquals(JToken.FromObject(Profiles), JToken.FromObject(config.Profiles)))
                    return true;
            }
            catch
            {
                return true;
            }

            if (!string.Equals(SelectedProfile?.Id, _activeProfileId, StringComparison.Ordinal)
                || McpEnabled != config.McpEnabled
                || McpPort != config.McpPort
                || !IsMcpPortValid
                || !string.Equals(McpBearerToken.Trim(), config.McpBearerToken, StringComparison.Ordinal)
                || !string.Equals(
                    ExternalMcpServersText,
                    CopilotMcpClientConfigurationText.Format(config.ExternalMcpServers),
                    StringComparison.Ordinal)
                || !IsExternalMcpServersValid
                || !string.Equals(WebPagePref64PrefixesText, config.WebPagePref64Prefixes, StringComparison.Ordinal)
                || !IsWebPagePref64PrefixesValid
                || !string.Equals(BackendSyncUrl.Trim(), config.BackendSyncUrl, StringComparison.Ordinal))
            {
                return true;
            }

            var defaults = config.AgentDefaults;
            if (AgentContextWindowTokens != defaults.ContextWindowTokens
                || AutoCompactConversationHistory != defaults.AutoCompactConversationHistory
                || AutoCompactThresholdPercent != defaults.AutoCompactThresholdPercent
                || !string.Equals(
                    CopilotAgentDefaultsConfig.NormalizeAutoCompactInstructions(AutoCompactInstructions),
                    defaults.AutoCompactInstructions,
                    StringComparison.Ordinal)
                || AgentRequestTokenBudget != defaults.RequestTokenBudget
                || MaxAgentToolCalls != defaults.MaxToolCalls
                || MaxAgentPasses != defaults.MaxAgentPasses
                || AgentTimeoutSeconds != defaults.TimeoutSeconds
                || PreferredShell != defaults.PreferredShell)
            {
                return true;
            }

            var draftSkillOverrides = CopilotAgentSkillOverrideConfig.Normalize(AgentSkillSettings
                .Where(setting => setting.State != CopilotAgentSkillOverrideState.Auto)
                .Select(setting => new CopilotAgentSkillOverrideConfig
                {
                    Name = setting.Name,
                    SkillFilePath = setting.SkillFilePath,
                    State = setting.State,
                }))
                .Select(item => (item.Name, item.SkillFilePath, item.State));
            var persistedSkillOverrides = CopilotAgentSkillOverrideConfig.Normalize(defaults.SkillOverrides)
                .Select(item => (item.Name, item.SkillFilePath, item.State));
            return !draftSkillOverrides.SequenceEqual(persistedSkillOverrides);
        }
    }
}
