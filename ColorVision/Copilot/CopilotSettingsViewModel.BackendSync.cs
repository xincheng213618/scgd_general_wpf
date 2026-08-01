using ColorVision.UI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        private async Task SyncBackendConfigAsync()
        {
            if (!CanSyncBackendConfig)
                return;

            IsSyncingBackendConfig = true;
            BackendSyncStatusText = "Downloading backend Copilot configuration...";
            SetSettingsNotice("Downloading backend Copilot configuration...");
            try
            {
                var response = await _backendSyncClient.FetchAsync(
                    BackendSyncUrl,
                    AllowInsecureBackendSync,
                    _lifetimeCancellation.Token);

                var previousSelectedId = SelectedProfile?.Id ?? string.Empty;
                CopilotBackendMergeResult mergeResult;
                _isApplyingPreset = true;
                _isSavingSettings = true;
                try
                {
                    mergeResult = CopilotBackendSyncClient.MergeProfiles(
                        Profiles,
                        response,
                        BackendSyncUrl);
                    if (Profiles.Count == 0)
                        Profiles.Add(CopilotProfileConfig.CreateDefault());

                    SelectedProfile = Profiles.FirstOrDefault(profile =>
                            string.Equals(profile.Id, mergeResult.DefaultLocalProfileId, StringComparison.Ordinal))
                        ?? Profiles.FirstOrDefault(profile =>
                            string.Equals(profile.Id, previousSelectedId, StringComparison.Ordinal))
                        ?? Profiles.FirstOrDefault(profile => profile.IsConfigured)
                        ?? Profiles.FirstOrDefault();
                }
                finally
                {
                    _isSavingSettings = false;
                    _isApplyingPreset = false;
                }

                RefreshSelectedProfileTestState("This profile was synchronized from the backend.");
                OnSelectedProfileUsageChanged();
                var revision = string.IsNullOrWhiteSpace(response.Revision) ? "unknown" : response.Revision;
                BackendSyncStatusText =
                    $"Revision {revision}: {mergeResult.Added} added, {mergeResult.Updated} updated, {mergeResult.Removed} removed and saved.";
                SaveSynchronizedProfiles();
                SetSettingsNotice(HasUnsavedSettings
                    ? BackendSyncStatusText + " Other settings still have unsaved changes."
                    : BackendSyncStatusText);
            }
            catch (OperationCanceledException) when (_disposed)
            {
            }
            catch (Exception ex)
            {
                BackendSyncStatusText = "Sync failed: " + SanitizeError(ex.Message);
                SetSettingsNotice(BackendSyncStatusText);
            }
            finally
            {
                IsSyncingBackendConfig = false;
            }
        }

        private void SaveSynchronizedProfiles()
        {
            var config = CopilotConfig.Instance;
            config.Profiles.Clear();
            foreach (var profile in Profiles.Select(profile => profile.Clone()))
            {
                profile.EnsureValid();
                config.Profiles.Add(profile);
            }

            config.EnsureInitialized();
            ConfigHandler.GetInstance().Save<CopilotConfig>();
            _activeProfileId = SelectedProfile?.Id ?? _activeProfileId;
            HasAppliedChanges = true;
        }
    }
}
