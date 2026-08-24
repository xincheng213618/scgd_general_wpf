using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotBackendSyncPlan(
        IReadOnlyList<CopilotProfileConfig> PersistedProfiles,
        IReadOnlyList<CopilotProfileConfig> DisplayProfiles,
        string SelectedProfileId,
        string ActiveProfileId,
        CopilotBackendMergeResult MergeResult);

    internal static class CopilotBackendSyncTransaction
    {
        public static CopilotBackendSyncPlan Prepare(
            CopilotConfig persistedConfig,
            IEnumerable<CopilotProfileConfig> displayProfiles,
            CopilotBackendConfigResponse response,
            string baseUrl,
            string? previousSelectedProfileId,
            string? currentActiveProfileId)
        {
            ArgumentNullException.ThrowIfNull(persistedConfig);
            ArgumentNullException.ThrowIfNull(displayProfiles);
            ArgumentNullException.ThrowIfNull(response);

            var persistedProfiles = new ObservableCollection<CopilotProfileConfig>(
                (persistedConfig.Profiles ?? new ObservableCollection<CopilotProfileConfig>())
                    .Where(profile => profile != null)
                    .Select(profile => profile.Clone()));
            var baselineProfileIds = persistedProfiles
                .Select(profile => profile.Id)
                .ToHashSet(StringComparer.Ordinal);
            var mergeResult = CopilotBackendSyncClient.MergeProfiles(
                persistedProfiles,
                response,
                baseUrl);
            if (persistedProfiles.Count == 0)
                persistedProfiles.Add(CopilotProfileConfig.CreateDefault());

            var syncSource = CopilotBackendSyncClient.BuildEndpoint(baseUrl)
                .GetLeftPart(UriPartial.Authority)
                .TrimEnd('/');
            var displayProfileSnapshot = ReconcileDisplayProfiles(
                displayProfiles,
                persistedProfiles,
                syncSource);
            if (displayProfileSnapshot.Count == 0)
            {
                var persistedFallback = persistedProfiles.FirstOrDefault(profile =>
                    !baselineProfileIds.Contains(profile.Id));
                displayProfileSnapshot.Add(
                    persistedFallback?.Clone()
                    ?? CopilotProfileConfig.CreateDefault());
            }

            var selectedProfile = displayProfileSnapshot.FirstOrDefault(profile =>
                    string.Equals(profile.Id, mergeResult.DefaultLocalProfileId, StringComparison.Ordinal))
                ?? displayProfileSnapshot.FirstOrDefault(profile =>
                    string.Equals(profile.Id, previousSelectedProfileId, StringComparison.Ordinal))
                ?? displayProfileSnapshot.FirstOrDefault(profile => profile.IsConfigured)
                ?? displayProfileSnapshot[0];
            var persistedProfileIds = persistedProfiles
                .Select(profile => profile.Id)
                .ToHashSet(StringComparer.Ordinal);
            var activeProfileId = persistedProfileIds.Contains(mergeResult.DefaultLocalProfileId)
                ? mergeResult.DefaultLocalProfileId
                : persistedProfileIds.Contains(currentActiveProfileId ?? string.Empty)
                    ? currentActiveProfileId!
                    : persistedProfileIds.Contains(selectedProfile.Id)
                        ? selectedProfile.Id
                        : persistedProfiles.FirstOrDefault(profile => profile.IsConfigured)?.Id
                            ?? persistedProfiles[0].Id;

            return new CopilotBackendSyncPlan(
                persistedProfiles.Select(profile => profile.Clone()).ToArray(),
                displayProfileSnapshot.Select(profile => profile.Clone()).ToArray(),
                selectedProfile.Id,
                activeProfileId,
                mergeResult);
        }

        public static ConfigSavePublicationStatus TryPersist(
            ConfigHandler configHandler,
            CopilotConfig liveConfig,
            CopilotBackendSyncPlan plan,
            out string errorMessage)
        {
            ArgumentNullException.ThrowIfNull(configHandler);
            ArgumentNullException.ThrowIfNull(liveConfig);
            ArgumentNullException.ThrowIfNull(plan);

            var candidate = liveConfig.CreatePersistenceSnapshot(plan.PersistedProfiles);
            var status = configHandler.TrySaveAndPublish(
                candidate,
                () => liveConfig.CommitProfiles(plan.PersistedProfiles),
                out errorMessage);
            if (status != ConfigSavePublicationStatus.PersistedAndPublished)
                return status;

            try
            {
                liveConfig.NotifyProfilesReplaced();
                return ConfigSavePublicationStatus.PersistedAndPublished;
            }
            catch (Exception ex)
            {
                errorMessage = ex.GetBaseException().Message;
                return ConfigSavePublicationStatus.PersistedButPublishFailed;
            }
        }

        private static List<CopilotProfileConfig> ReconcileDisplayProfiles(
            IEnumerable<CopilotProfileConfig> displayProfiles,
            IEnumerable<CopilotProfileConfig> persistedProfiles,
            string syncSource)
        {
            var managedProfiles = persistedProfiles
                .Where(profile => IsManagedBy(profile, syncSource)
                    && !string.IsNullOrWhiteSpace(profile.SyncProfileId))
                .GroupBy(profile => profile.SyncProfileId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var reconciled = new List<CopilotProfileConfig>();
            var managedProfilesEmitted = false;

            foreach (var profile in displayProfiles.Where(profile => profile != null))
            {
                if (!IsManagedBy(profile, syncSource))
                {
                    reconciled.Add(profile.Clone());
                    continue;
                }

                if (!managedProfilesEmitted)
                {
                    reconciled.AddRange(managedProfiles.Select(item => item.Clone()));
                    managedProfilesEmitted = true;
                }
            }

            if (!managedProfilesEmitted)
                reconciled.AddRange(managedProfiles.Select(profile => profile.Clone()));

            return reconciled;
        }

        private static bool IsManagedBy(CopilotProfileConfig profile, string syncSource) =>
            string.Equals(profile.SyncSource, syncSource, StringComparison.OrdinalIgnoreCase);
    }
}
