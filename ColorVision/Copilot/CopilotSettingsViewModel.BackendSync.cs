using ColorVision.UI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        internal async Task SyncBackendConfigAsync()
        {
            if (!EnsureCurrentConfigGeneration() || !CanSyncBackendConfig)
                return;

            IsSyncingBackendConfig = true;
            BackendSyncStatusText = "Downloading backend Copilot configuration...";
            SetSettingsNotice("Downloading backend Copilot configuration...");
            try
            {
                var response = await _fetchBackendConfigAsync(
                    BackendSyncUrl,
                    AllowInsecureBackendSync,
                    _lifetimeCancellation.Token);
                if (!EnsureCurrentConfigGeneration())
                    return;

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
                if (!SaveSynchronizedProfiles())
                    return;

                SetSettingsNotice(HasUnsavedSettings
                    ? BackendSyncStatusText + " Other settings still have unsaved changes."
                    : BackendSyncStatusText);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
                EnsureCurrentConfigGeneration();
            }
            catch (Exception ex)
            {
                if (!EnsureCurrentConfigGeneration())
                    return;

                BackendSyncStatusText = "Sync failed: " + SanitizeError(ex.Message);
                SetSettingsNotice(BackendSyncStatusText);
            }
            finally
            {
                IsSyncingBackendConfig = false;
            }
        }

        private bool SaveSynchronizedProfiles()
        {
            if (!EnsureCurrentConfigGeneration())
                return false;

            var config = _sourceConfig;
            config.Profiles.Clear();
            foreach (var profile in Profiles.Select(profile => profile.Clone()))
            {
                profile.EnsureValid();
                config.Profiles.Add(profile);
            }

            config.EnsureInitialized();
            if (!EnsureCurrentConfigGeneration())
                return false;

            _persistConfig();
            if (!EnsureCurrentConfigGeneration())
                return false;

            _activeProfileId = SelectedProfile?.Id ?? _activeProfileId;
            HasAppliedChanges = true;
            return true;
        }
    }
}
