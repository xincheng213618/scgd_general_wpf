using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotChatPanel
    {
        private sealed record ProfileSelectorProvider(string Id, string Label, string Kind, CopilotProfileConfig[] Profiles);

        private string? _browsedProviderId;
        private bool _updatingProfileSelector;
        private IReadOnlyList<CopilotReasoningOption> _selectorReasoningOptions = Array.Empty<CopilotReasoningOption>();

        private void ProfileSelectorPopup_Opened(object sender, EventArgs e)
        {
            _browsedProviderId = (DataContext as CopilotChatViewModel)?.SelectedProfile?.EffectiveConnectionId;
            ProviderSelectorButton.IsChecked = false;
            ProviderMenuCard.Visibility = Visibility.Collapsed;
            RefreshProfileSelector();
        }

        private void ProfileSelectorPopup_Closed(object sender, EventArgs e)
        {
            ProfileSelectorButton.IsChecked = false;
            ProviderSelectorButton.IsChecked = false;
            ProviderMenuCard.Visibility = Visibility.Collapsed;
        }

        private void RefreshProfileSelector()
        {
            if (_updatingProfileSelector || DataContext is not CopilotChatViewModel viewModel)
                return;
            if (!viewModel.CanSelectProfile)
            {
                CloseProfileSelectorPopup();
                return;
            }

            _updatingProfileSelector = true;
            try
            {
                var providers = viewModel.Profiles.Where(profile => profile.IsAvailable)
                    .GroupBy(profile => profile.EffectiveConnectionId, StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var first = group.First();
                        return new ProfileSelectorProvider(group.Key,
                            string.IsNullOrWhiteSpace(first.ConnectionName)
                                ? first.IsLocalCodex ? DisplayText.Get("本机 Codex") : first.VendorLabel
                                : first.ConnectionName,
                            first.IsLocalCodex ? DisplayText.Get("本地") : "API", group.ToArray());
                    }).ToArray();
                var provider = providers.FirstOrDefault(item => string.Equals(item.Id, _browsedProviderId, StringComparison.OrdinalIgnoreCase))
                    ?? providers.FirstOrDefault();
                _browsedProviderId = provider?.Id;
                ProviderListBox.ItemsSource = providers;
                ProviderListBox.SelectedItem = provider;
                ProviderNameTextBlock.Text = provider?.Label ?? DisplayText.Get("选择供应商");
                ProviderNameTextBlock.ToolTip = provider?.Label;
                ProviderKindTextBlock.Text = provider?.Kind;
                // This list has its own selection. Browsing another connection must not clear the active profile.
                ProfileListBox.ItemsSource = provider?.Profiles;
                ProfileListBox.SelectedItem = provider?.Profiles.FirstOrDefault(item => item.Id == viewModel.SelectedProfile?.Id);
                _selectorReasoningOptions = viewModel.SelectedProfileReasoningOptions;
                ReasoningSelectorSection.Visibility = viewModel.HasConfigurableReasoning
                    && string.Equals(provider?.Id, viewModel.SelectedProfile?.EffectiveConnectionId, StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
                ReasoningSlider.Maximum = Math.Max(0, _selectorReasoningOptions.Count - 1);
                ReasoningSlider.Value = Math.Max(0, _selectorReasoningOptions.ToList().FindIndex(option => option.IsSelected));
                UpdateReasoningPreview();
            }
            finally
            {
                _updatingProfileSelector = false;
            }
            AlignProfileSelector();
        }

        private void AlignProfileSelector()
        {
            ProfileSelectorContent.UpdateLayout();
            ProfileSelectorPopup.HorizontalOffset = ProfileSelectorButton.ActualWidth - ProfileSelectorContent.DesiredSize.Width + 10;
        }

        private void ProviderSelectorButton_Click(object sender, RoutedEventArgs e)
        {
            ProviderMenuCard.Visibility = ProviderSelectorButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            AlignProfileSelector();
            if (ProviderMenuCard.IsVisible)
                ProviderListBox.Focus();
        }

        private void ProviderListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(ProviderListBox, source) is ListBoxItem { DataContext: ProfileSelectorProvider provider })
            {
                BrowseProvider(provider);
                e.Handled = true;
            }
        }

        private void BrowseProvider(ProfileSelectorProvider provider)
        {
            _browsedProviderId = provider.Id;
            ProviderSelectorButton.IsChecked = false;
            ProviderMenuCard.Visibility = Visibility.Collapsed;
            RefreshProfileSelector();
            ProfileListBox.Focus();
        }

        private void ManageModelsButton_Click(object sender, RoutedEventArgs e)
        {
            CloseProfileSelectorPopup();
            if (DataContext is CopilotChatViewModel viewModel && viewModel.OpenSettingsCommand.CanExecute(null))
                viewModel.OpenSettingsCommand.Execute(null);
        }

        private void ProfileListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(ProfileListBox, source) is ListBoxItem { DataContext: CopilotProfileConfig profile })
            {
                SelectProfile(profile);
                e.Handled = true;
            }
        }

        private void SelectProfile(CopilotProfileConfig profile)
        {
            if (profile.IsAvailable && DataContext is CopilotChatViewModel { CanSelectProfile: true } viewModel)
            {
                viewModel.SelectedProfile = profile;
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(CloseProfileSelectorPopup));
            }
        }

        private void UpdateReasoningPreview()
        {
            var index = (int)Math.Round(ReasoningSlider.Value);
            if (index < 0 || index >= _selectorReasoningOptions.Count)
                return;
            var option = _selectorReasoningOptions[index];
            ReasoningValueTextBlock.Text = option.Label;
            ReasoningSlider.ToolTip = option.Description;
            System.Windows.Automation.AutomationProperties.SetHelpText(ReasoningSlider, option.Label + ": " + option.Description);
        }

        private void ReasoningSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingProfileSelector || ReasoningValueTextBlock == null)
                return;
            UpdateReasoningPreview();
            if (!ReasoningSlider.IsMouseCaptureWithin && Mouse.LeftButton != MouseButtonState.Pressed)
                CommitReasoningSelection();
        }

        private void ReasoningSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => CommitReasoningSelection();

        private void ReasoningSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!e.Canceled)
                CommitReasoningSelection();
        }

        private void CommitReasoningSelection()
        {
            var index = (int)Math.Round(ReasoningSlider.Value);
            if (_updatingProfileSelector || index < 0 || index >= _selectorReasoningOptions.Count
                || DataContext is not CopilotChatViewModel { CanSelectProfile: true } viewModel)
                return;
            viewModel.SetSelectedProfileReasoningMode(_selectorReasoningOptions[index].Mode);
            // Re-read persisted state, including rollback after a failed save.
            RefreshProfileSelector();
        }

        private void ProfileSelectorContent_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (ProviderMenuCard.Visibility == Visibility.Visible)
                {
                    ProviderSelectorButton.IsChecked = false;
                    ProviderMenuCard.Visibility = Visibility.Collapsed;
                    AlignProfileSelector();
                    ProviderSelectorButton.Focus();
                }
                else
                {
                    CloseProfileSelectorPopup();
                    ProfileSelectorButton.Focus();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && ProviderListBox.IsKeyboardFocusWithin && ProviderListBox.SelectedItem is ProfileSelectorProvider provider)
            {
                BrowseProvider(provider);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && ProfileListBox.IsKeyboardFocusWithin && ProfileListBox.SelectedItem is CopilotProfileConfig profile)
            {
                SelectProfile(profile);
                e.Handled = true;
            }
            else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control && ReasoningSelectorSection.Visibility == Visibility.Visible)
            {
                ReasoningSlider.Value = ((int)ReasoningSlider.Value + 1) % _selectorReasoningOptions.Count;
                ReasoningSlider.Focus();
                e.Handled = true;
            }
        }

        private bool OpenProfileSelector()
        {
            if (DataContext is not CopilotChatViewModel { CanSelectProfile: true } viewModel)
                return false;
            ProfileSelectorButton.IsChecked = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!ProfileSelectorPopup.IsOpen)
                    return;
                ProfileListBox.Focus();
                if (viewModel.SelectedProfile != null)
                    ProfileListBox.ScrollIntoView(viewModel.SelectedProfile);
            });
            return true;
        }

        private bool OpenReasoningSelector()
        {
            if (DataContext is not CopilotChatViewModel { CanSelectProfile: true, HasConfigurableReasoning: true })
                return false;
            ProfileSelectorButton.IsChecked = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (ProfileSelectorPopup.IsOpen)
                    ReasoningSlider.Focus();
            });
            return true;
        }

        private void CloseProfileSelectorPopup()
        {
            if (ProfileSelectorPopup != null)
                ProfileSelectorPopup.IsOpen = false;
        }
    }
}
