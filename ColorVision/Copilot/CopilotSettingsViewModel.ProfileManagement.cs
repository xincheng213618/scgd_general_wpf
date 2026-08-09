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

            _isSavingSettings = true;
            try
            {
                var config = CopilotConfig.Instance;
                config.Profiles.Clear();
                foreach (var profile in Profiles.Select(profile => profile.Clone()))
                {
                    profile.EnsureValid();
                    config.Profiles.Add(profile);
                }

                config.McpEnabled = McpEnabled;
                config.AgentDefaults.ContextWindowTokens = AgentContextWindowTokens;
                config.AgentDefaults.AutoCompactConversationHistory = AutoCompactConversationHistory;
                config.AgentDefaults.AutoCompactThresholdPercent = AutoCompactThresholdPercent;
                config.AgentDefaults.AutoCompactInstructions = AutoCompactInstructions;
                config.AgentDefaults.RequestTokenBudget = AgentRequestTokenBudget;
                config.AgentDefaults.MaxToolCalls = MaxAgentToolCalls;
                config.AgentDefaults.MaxAgentPasses = MaxAgentPasses;
                config.AgentDefaults.TimeoutSeconds = AgentTimeoutSeconds;
                config.AgentDefaults.PreferredShell = PreferredShell;
                config.AgentDefaults.SkillOverrides.Clear();
                foreach (var item in CopilotAgentSkillOverrideConfig.Normalize(AgentSkillSettings
                    .Where(setting => setting.State != CopilotAgentSkillOverrideState.Auto)
                    .Select(setting => new CopilotAgentSkillOverrideConfig
                    {
                        Name = setting.Name,
                        SkillFilePath = setting.SkillFilePath,
                        State = setting.State,
                    })))
                {
                    config.AgentDefaults.SkillOverrides.Add(item);
                }
                config.McpPort = McpPort;
                config.McpBearerToken = string.IsNullOrWhiteSpace(McpBearerToken)
                    ? CopilotConfig.GenerateMcpBearerToken()
                    : McpBearerToken.Trim();
                config.ExternalMcpServers.Clear();
                foreach (var server in externalMcpServers)
                    config.ExternalMcpServers.Add(server.Clone());
                config.BackendSyncUrl = BackendSyncUrl.Trim();
                config.AllowInsecureBackendSync = AllowInsecureBackendSync;

                config.EnsureInitialized();
                McpPort = config.McpPort;
                McpPortText = config.McpPort.ToString(CultureInfo.InvariantCulture);
                McpEndpoint = BuildMcpEndpoint();
                McpBearerToken = config.McpBearerToken;
                ConfigHandler.GetInstance().Save<CopilotConfig>();
                CopilotMcpServer.Instance.ApplyConfig();
                RefreshMcpStatusText();
                RefreshMcpDiagnostics();
                _activeProfileId = SelectedProfile?.Id ?? _activeProfileId;
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
            if (_isApplyingPreset || sender is not CopilotProfileConfig profile)
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

            if (IsTestingSelectedProfileConnection)
                return;

            SelectedProfileConnectionTestText = SelectedProfile?.IsConfigured == true
                ? string.IsNullOrWhiteSpace(configuredMessage)
                    ? "Test sends one short request using the selected profile."
                    : configuredMessage
                : "Complete API key, endpoint, and model before testing.";
        }

        private void ToggleSelectedProfileConnectionTest()
        {
            if (IsTestingSelectedProfileConnection)
            {
                var cancellation = _modelConnectionTestCancellation;
                if (cancellation == null || cancellation.IsCancellationRequested)
                    return;

                cancellation.Cancel();
                SelectedProfileConnectionTestText = "Cancelling model connection test...";
                SetSettingsNotice(SelectedProfileConnectionTestText);
                return;
            }

            RunUiOperation(TestSelectedProfileConnectionAsync, "测试模型连接");
        }

        private async Task TestSelectedProfileConnectionAsync()
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
            _modelConnectionTestCancellation = cancellation;
            IsTestingSelectedProfileConnection = true;
            SelectedProfileConnectionTestText = "Testing model connection...";
            SetSettingsNotice($"Testing {sourceProfile.DisplayLabel}...");
            try
            {
                var result = await _modelConnectionDiagnostic.TestAsync(
                    sourceProfile,
                    cancellation.Token);
                SelectedProfileConnectionTestText = result.FormatStatus();
                SetSettingsNotice($"Model test succeeded for {sourceProfile.DisplayLabel}. {SelectedProfileConnectionTestText}");
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException)
            {
                SelectedProfileConnectionTestText = "Connection test cancelled.";
                SetSettingsNotice(SelectedProfileConnectionTestText);
            }
            catch (CopilotModelConnectionDiagnosticException exception)
            {
                SelectedProfileConnectionTestText = FormatModelConnectionDiagnosticFailure(exception);
                SetSettingsNotice(SelectedProfileConnectionTestText);
            }
            catch (Exception ex)
            {
                SelectedProfileConnectionTestText = "Connection failed: " + SanitizeError(ex.Message);
                SetSettingsNotice(SanitizeError(SelectedProfileConnectionTestText));
            }
            finally
            {
                if (ReferenceEquals(_modelConnectionTestCancellation, cancellation))
                    _modelConnectionTestCancellation = null;
                IsTestingSelectedProfileConnection = false;
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
