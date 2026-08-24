using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private ConfigSavePublicationStatus TryPersistCurrentConfig(out string errorMessage)
        {
            if (!TryCreateConfigCandidate(out var candidate, out errorMessage))
                return ConfigSavePublicationStatus.NotPersisted;

            return TryPersistConfigCandidate(
                candidate,
                refreshViewModel: false,
                out errorMessage);
        }

        private ConfigSavePublicationStatus TryPersistConfigMutation(
            Action<CopilotConfig> mutation,
            out string errorMessage)
        {
            ArgumentNullException.ThrowIfNull(mutation);
            if (!TryCreateConfigCandidate(out var candidate, out errorMessage))
                return ConfigSavePublicationStatus.NotPersisted;

            try
            {
                mutation(candidate);
                candidate.EnsureInitialized();
            }
            catch (Exception ex)
            {
                errorMessage = ex.GetBaseException().Message;
                return ConfigSavePublicationStatus.NotPersisted;
            }

            return TryPersistConfigCandidate(
                candidate,
                refreshViewModel: true,
                out errorMessage);
        }

        internal ConfigSavePublicationStatus TryPersistSkillMcpDependencyPlan(
            CopilotAgentSkillMcpDependencyInstallPlan plan,
            out IReadOnlyList<CopilotMcpClientServerConfig> addedServers,
            out string errorMessage)
        {
            ArgumentNullException.ThrowIfNull(plan);
            addedServers = Array.Empty<CopilotMcpClientServerConfig>();
            if (!TryCreateConfigCandidate(out var candidate, out errorMessage))
                return ConfigSavePublicationStatus.NotPersisted;

            if (!CopilotAgentSkillMcpDependencyInstaller.TryInstall(
                    plan,
                    candidate.ExternalMcpServers,
                    out var candidateAdditions,
                    out errorMessage))
            {
                return ConfigSavePublicationStatus.NotPersisted;
            }

            var additionSnapshot = candidateAdditions.Select(server => server.Clone()).ToArray();
            var status = TryPersistConfigCandidate(
                candidate,
                refreshViewModel: true,
                out errorMessage);
            if (status == ConfigSavePublicationStatus.PersistedAndPublished)
                addedServers = additionSnapshot;
            return status;
        }

        private bool TryCreateConfigCandidate(
            out CopilotConfig candidate,
            out string errorMessage)
        {
            try
            {
                candidate = _config.CreatePersistenceSnapshot();
                candidate.EnsureInitialized();
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                candidate = null!;
                errorMessage = ex.GetBaseException().Message;
                return false;
            }
        }

        private ConfigSavePublicationStatus TryPersistConfigCandidate(
            CopilotConfig candidate,
            bool refreshViewModel,
            out string errorMessage)
        {
            var preferredProfileId = refreshViewModel
                ? SelectedProfile?.Id ?? _state.ActiveProfileId
                : string.Empty;
            ConfigSavePublicationStatus status;
            if (_configHandler == null)
            {
                try
                {
                    _config.CommitPersistenceSnapshot(candidate);
                    errorMessage = string.Empty;
                    status = ConfigSavePublicationStatus.PersistedAndPublished;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.GetBaseException().Message;
                    return ConfigSavePublicationStatus.PersistedButPublishFailed;
                }
            }
            else
            {
                status = _configHandler.TrySaveAndPublish(
                    candidate,
                    () => _config.CommitPersistenceSnapshot(candidate),
                    out errorMessage);
            }
            if (status != ConfigSavePublicationStatus.PersistedAndPublished)
                return status;

            try
            {
                _config.NotifyPersistenceSnapshotApplied();
                if (refreshViewModel)
                    ReloadStateFromConfig(preferredProfileId);
                return ConfigSavePublicationStatus.PersistedAndPublished;
            }
            catch (Exception ex)
            {
                errorMessage = ex.GetBaseException().Message;
                return ConfigSavePublicationStatus.PersistedButPublishFailed;
            }
        }
    }
}
