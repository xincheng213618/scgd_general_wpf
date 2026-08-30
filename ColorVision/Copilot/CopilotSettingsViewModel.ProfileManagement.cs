#pragma warning disable CA1822
using ColorVision.Common.MVVM;
using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        public bool Save()
        {
            if (!ApplyMcpPortText(updateNotice: true))
                return false;
            if (!CopilotMcpClientConfigurationText.TryParse(ExternalMcpServersText, out var externalMcpServers, out var externalMcpError))
            {
                IsExternalMcpServersValid = false;
                ExternalMcpServersValidationText = externalMcpError;
                SetSettingsNotice(externalMcpError);
                return false;
            }
            if (!CopilotWebPagePref64Configuration.TryParse(WebPagePref64PrefixesText, out var webPagePref64Prefixes, out var webPagePref64Error))
            {
                IsWebPagePref64PrefixesValid = false;
                WebPagePref64PrefixesValidationText = webPagePref64Error;
                SetSettingsNotice(webPagePref64Error);
                return false;
            }

            _isSavingSettings = true;
            var persisted = false;
            try
            {
                var config = _config;
                var selectedProfileId = SelectedProfile?.Id ?? string.Empty;
                var candidate = config.CreatePersistenceSnapshot(Profiles);

                candidate.McpEnabled = McpEnabled;
                candidate.AgentDefaults.ContextWindowTokens = AgentContextWindowTokens;
                candidate.AgentDefaults.AutoCompactConversationHistory = AutoCompactConversationHistory;
                candidate.AgentDefaults.AutoCompactThresholdPercent = AutoCompactThresholdPercent;
                candidate.AgentDefaults.AutoCompactInstructions = AutoCompactInstructions;
                candidate.AgentDefaults.RequestTokenBudget = AgentRequestTokenBudget;
                candidate.AgentDefaults.MaxToolCalls = MaxAgentToolCalls;
                candidate.AgentDefaults.MaxAgentPasses = MaxAgentPasses;
                candidate.AgentDefaults.TimeoutSeconds = AgentTimeoutSeconds;
                candidate.AgentDefaults.PreferredShell = PreferredShell;
                candidate.AgentDefaults.SkillOverrides.Clear();
                foreach (var item in CopilotAgentSkillOverrideConfig.Normalize(AgentSkillSettings
                    .Where(setting => setting.State != CopilotAgentSkillOverrideState.Auto)
                    .Select(setting => new CopilotAgentSkillOverrideConfig
                    {
                        Name = setting.Name,
                        SkillFilePath = setting.SkillFilePath,
                        State = setting.State,
                    })))
                {
                    candidate.AgentDefaults.SkillOverrides.Add(item);
                }
                candidate.McpPort = McpPort;
                candidate.McpBearerToken = string.IsNullOrWhiteSpace(McpBearerToken)
                    ? CopilotConfig.GenerateMcpBearerToken()
                    : McpBearerToken.Trim();
                candidate.ExternalMcpServers.Clear();
                foreach (var server in externalMcpServers)
                    candidate.ExternalMcpServers.Add(server.Clone());
                candidate.WebPagePref64Prefixes = CopilotWebPagePref64Configuration.Format(webPagePref64Prefixes);
                candidate.BackendSyncUrl = BackendSyncUrl.Trim();

                candidate.EnsureInitialized();
                var persistenceStatus = _configHandler.TrySaveAndPublish(
                        candidate,
                        () => config.CommitPersistenceSnapshot(candidate),
                        out var persistenceError);
                if (persistenceStatus == ConfigSavePublicationStatus.NotPersisted)
                {
                    SetSettingsNotice("Settings were not saved. " + SanitizeError(persistenceError));
                    return false;
                }

                persisted = true;
                if (persistenceStatus == ConfigSavePublicationStatus.PersistedButPublishFailed)
                {
                    HasAppliedChanges = true;
                    HasUnsavedSettings = true;
                    SetSettingsNotice(
                        "Settings were saved, but the running application could not refresh. "
                        + SanitizeError(persistenceError));
                    return false;
                }
                config.NotifyPersistenceSnapshotApplied();
                Profiles.Clear();
                foreach (var profile in config.Profiles.Select(profile => profile.Clone()))
                    Profiles.Add(profile);
                SelectedProfile = Profiles.FirstOrDefault(profile =>
                        string.Equals(profile.Id, selectedProfileId, StringComparison.Ordinal))
                    ?? Profiles.FirstOrDefault(profile => profile.IsConfigured)
                    ?? Profiles.FirstOrDefault();
                McpPort = config.McpPort;
                McpPortText = config.McpPort.ToString(CultureInfo.InvariantCulture);
                McpEndpoint = BuildMcpEndpoint();
                McpBearerToken = config.McpBearerToken;
                WebPagePref64PrefixesText = config.WebPagePref64Prefixes;
                BackendSyncUrl = config.BackendSyncUrl;
                CopilotMcpServer.Instance.ApplySettings(new CopilotMcpRuntimeSettings
                {
                    Enabled = config.McpEnabled,
                    Host = "127.0.0.1",
                    Port = config.McpPort,
                    BearerToken = config.McpBearerToken,
                });
                RefreshMcpStatusText();
                RefreshMcpDiagnostics();
                SetActiveProfileId(SelectedProfile?.Id);
            }
            catch (Exception ex)
            {
                if (persisted)
                {
                    HasAppliedChanges = true;
                    HasUnsavedSettings = true;
                    SetSettingsNotice("Settings were saved, but the runtime refresh failed. " + SanitizeError(ex.Message));
                }
                else
                {
                    SetSettingsNotice("Settings were not saved. " + SanitizeError(ex.Message));
                }
                return false;
            }
            finally
            {
                _isSavingSettings = false;
            }

            MarkSettingsSaved();
            return true;
        }

        private void UseSelectedProfileInChat()
        {
            var profile = SelectedProfile;
            if (profile == null)
            {
                SetSettingsNotice("Select a model profile before using it in chat.");
                return;
            }

            if (!profile.IsConfigured)
            {
                SetSettingsNotice("Complete API key, endpoint, and model before using this profile in chat.");
                OnSelectedProfileUsageChanged();
                return;
            }

            var displayLabel = profile.DisplayLabel;
            if (Save())
                SetSettingsNotice($"{displayLabel} is active in chat.");
        }

        public void PrepareAddModelDialog()
        {
            ClearQuickAddFeedback();
            ClearQuickAddCredentialDraft();
            ConnectProviderSearchText = string.Empty;
            IsConnectProviderPickerVisible = true;
        }

        public void ClearQuickAddModelDraft()
        {
            ClearQuickAddFeedback();
            ClearQuickAddCredentialDraft();
        }

        private void SelectConnectProvider(CopilotConnectProviderOption? option)
        {
            if (option == null)
                return;

            if (NewProfileVendorType != option.VendorType)
                ClearQuickAddCredentialDraft();

            NewProfileVendorType = option.VendorType;
            IsConnectProviderPickerVisible = false;
            ClearQuickAddFeedback();
        }

        public bool AddQuickProfile(bool useNow)
        {
            if (useNow && !CanSaveSettings)
            {
                SetSettingsNotice("Fix the MCP port before adding and using a model.");
                return false;
            }

            var profile = AddProfileCore();
            if (profile == null)
                return false;

            if (!useNow)
            {
                NewProfileAddFeedbackText = $"Added {profile.DisplayLabel}. It is saved after Apply or Save.";
                MarkSettingsPending($"Added {profile.DisplayLabel}. Click Apply to use it in chat, or Save to close.");
                return true;
            }

            var displayLabel = profile.DisplayLabel;
            if (!Save())
                return false;

            NewProfileAddFeedbackText = $"Ready: {displayLabel} is active in chat. You can close settings now.";
            SetSettingsNotice($"{displayLabel} is active in chat. You can close settings now.");
            return true;
        }

        private void AddProfile()
        {
            AddQuickProfile(useNow: false);
        }

        private void AddAndUseProfile()
        {
            AddQuickProfile(useNow: true);
        }

        private CopilotProfileConfig? AddProfileCore()
        {
            if (!CanAddProfile)
                return null;

            var profile = CreateProfileForVendor(NewProfileVendorType);
            profile.ApiKey = NewProfileApiKey.Trim();
            Profiles.Add(profile);
            SelectedProfile = profile;
            NewProfileApiKey = string.Empty;
            IsNewProfileApiKeyVisible = false;
            return profile;
        }

        private void DuplicateSelectedProfile()
        {
            if (SelectedProfile == null)
                return;

            var profile = SelectedProfile.Clone();
            profile.Id = Guid.NewGuid().ToString("N");
            profile.Name = $"{SelectedProfile.DisplayLabel} Copy";
            profile.SyncSource = string.Empty;
            profile.SyncProfileId = string.Empty;
            Profiles.Add(profile);
            SelectedProfile = profile;
            MarkSettingsPending($"Duplicated {SelectedProfile.DisplayLabel}. Click Apply or Save to keep it.");
        }

        private void DeleteSelectedProfile()
        {
            if (SelectedProfile == null)
                return;

            var index = Profiles.IndexOf(SelectedProfile);
            Profiles.Remove(SelectedProfile);

            if (Profiles.Count == 0)
                Profiles.Add(CopilotProfileConfig.CreateDefault());

            SelectedProfile = Profiles[Math.Clamp(index, 0, Profiles.Count - 1)];
            MarkSettingsPending("Profile list changed. Click Apply or Save to keep it.");
        }

        private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not CopilotProfileConfig profile)
                return;

            InvalidateSelectedProfileConnectionTest();
            if (_isApplyingPreset)
                return;

            if (e.PropertyName == nameof(CopilotProfileConfig.VendorType))
            {
                ApplyVendorPreset(profile, resetName: false);
                OnPropertyChanged(nameof(AvailableModelPresets));
            }
            else if (e.PropertyName == nameof(CopilotProfileConfig.ProviderType))
            {
                ApplyProviderPreset(profile);
            }

            RefreshSelectedProfileTestState("Profile details changed. Test uses the current unsaved values.");
            OnSelectedProfileUsageChanged();
            MarkSettingsPending("Profile details changed. Click Apply or Save to use them.");
        }

        private void RefreshSelectedProfileTestState(string? configuredMessage = null)
        {
            OnPropertyChanged(nameof(CanTestSelectedProfile));
            CommandManager.InvalidateRequerySuggested();

            if (IsTestingSelectedProfileConnection && _modelConnectionTestCancellation?.IsCancellationRequested != true)
                return;

            SelectedProfileConnectionTestText = SelectedProfile?.IsConfigured == true
                ? string.IsNullOrWhiteSpace(configuredMessage)
                    ? "Test sends one short request using the selected profile."
                    : configuredMessage
                : "Complete API key, endpoint, and model before testing.";
        }

        private void InvalidateSelectedProfileConnectionTest()
        {
            _selectedProfileConnectionRevision++;
            var cancellation = _modelConnectionTestCancellation;
            if (cancellation == null)
                return;

            if (string.Equals(SettingsStatusText, _modelConnectionTestNotice, StringComparison.Ordinal))
                SetSettingsNotice("Model profile changed. Run a new connection test for the current values.");
            cancellation.Cancel();
        }

        private void ToggleSelectedProfileConnectionTest()
        {
            if (IsTestingSelectedProfileConnection)
            {
                var cancellation = _modelConnectionTestCancellation;
                if (cancellation == null || cancellation.IsCancellationRequested)
                    return;

                SelectedProfileConnectionTestText = "Cancelling model connection test...";
                _modelConnectionTestNotice = SelectedProfileConnectionTestText;
                SetSettingsNotice(_modelConnectionTestNotice);
                cancellation.Cancel();
                return;
            }

            RunUiOperation(TestSelectedProfileConnectionAsync, "测试模型连接");
        }

        internal async Task TestSelectedProfileConnectionAsync()
        {
            if (_disposed || IsTestingSelectedProfileConnection)
                return;

            var sourceProfile = SelectedProfile;
            if (sourceProfile == null)
            {
                SelectedProfileConnectionTestText = "Select a profile before testing.";
                SetSettingsNotice("Select a model profile before testing.");
                return;
            }

            if (!sourceProfile.IsConfigured)
            {
                SelectedProfileConnectionTestText = "Complete API key, endpoint, and model before testing.";
                SetSettingsNotice("Model test skipped: profile is incomplete.");
                RefreshSelectedProfileTestState();
                return;
            }

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            var profileRevision = _selectedProfileConnectionRevision;
            var profileLabel = sourceProfile.DisplayLabel;
            bool IsCurrentProfile() => !_disposed
                && ReferenceEquals(SelectedProfile, sourceProfile)
                && _selectedProfileConnectionRevision == profileRevision;
            _modelConnectionTestCancellation = cancellation;
            _modelConnectionTestNotice = $"Testing {profileLabel}...";
            IsTestingSelectedProfileConnection = true;
            SelectedProfileConnectionTestText = "Testing model connection...";
            SetSettingsNotice(_modelConnectionTestNotice);
            try
            {
                var result = await _modelConnectionDiagnostic.TestAsync(
                    sourceProfile,
                    cancellation.Token);
                if (!IsCurrentProfile() || cancellation.IsCancellationRequested)
                    return;
                SelectedProfileConnectionTestText = result.FormatStatus();
                SetSettingsNotice($"Model test succeeded for {profileLabel}. {SelectedProfileConnectionTestText}");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (CopilotModelConnectionDiagnosticException exception)
            {
                if (!IsCurrentProfile() || cancellation.IsCancellationRequested)
                    return;
                SelectedProfileConnectionTestText = FormatModelConnectionDiagnosticFailure(exception);
                SetSettingsNotice(SelectedProfileConnectionTestText);
            }
            catch (Exception ex)
            {
                if (!IsCurrentProfile() || cancellation.IsCancellationRequested)
                    return;
                SelectedProfileConnectionTestText = "Connection failed: " + SanitizeError(ex.Message);
                SetSettingsNotice(SanitizeError(SelectedProfileConnectionTestText));
            }
            finally
            {
                var ownsSettingsNotice = ReferenceEquals(_modelConnectionTestCancellation, cancellation)
                    && string.Equals(SettingsStatusText, _modelConnectionTestNotice, StringComparison.Ordinal);
                if (ReferenceEquals(_modelConnectionTestCancellation, cancellation))
                {
                    _modelConnectionTestCancellation = null;
                    _modelConnectionTestNotice = string.Empty;
                }
                IsTestingSelectedProfileConnection = false;
                if (IsCurrentProfile() && cancellation.IsCancellationRequested)
                {
                    SelectedProfileConnectionTestText = "Connection test cancelled.";
                    if (ownsSettingsNotice)
                        SetSettingsNotice(SelectedProfileConnectionTestText);
                }
                else if (!_disposed && !IsCurrentProfile())
                {
                    RefreshSelectedProfileTestState("Profile details changed. Test uses the current unsaved values.");
                }
            }
        }

        internal static string FormatModelConnectionDiagnosticFailure(
            CopilotModelConnectionDiagnosticException exception)
        {
            var elapsed = CopilotModelConnectionDiagnosticResult.FormatDuration(exception.Elapsed);
            var retrySummary = exception.RetryCount switch
            {
                <= 0 => string.Empty,
                1 => " after 1 automatic retry",
                _ => $" after {exception.RetryCount.ToString("N0", CultureInfo.InvariantCulture)} automatic retries",
            };
            if (CopilotProviderInactivityException.TryFind(exception.InnerException, out var inactivity))
            {
                var phase = inactivity.Phase == CopilotProviderInactivityPhase.FirstResponse
                    ? "no displayable content arrived"
                    : "the provider stream stopped updating";
                var timeout = CopilotModelConnectionDiagnosticResult.FormatDuration(
                    inactivity.TimeoutDuration);
                return $"Connection failed in {elapsed}{retrySummary}: {phase} for {timeout}.";
            }

            var message = exception.InnerException?.Message ?? exception.Message;
            return $"Connection failed in {elapsed}{retrySummary}: {SanitizeError(message)}";
        }
    }
}
