using ColorVision.Themes;
using System;
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
        private void ProfileSelectorPopup_Opened(object sender, EventArgs e)
        {
            SetProfileSelectorSubmenu(modelVisible: false, reasoningVisible: false);
        }

        private void ProfileSelectorPopup_Closed(object sender, EventArgs e)
        {
            ProfileSelectorButton.IsChecked = false;
            SetProfileSelectorSubmenu(modelVisible: false, reasoningVisible: false);
        }

        private void ModelSelectorRowButton_Click(object sender, RoutedEventArgs e)
        {
            SetProfileSelectorSubmenu(modelVisible: ModelSelectorRowButton.IsChecked == true, reasoningVisible: false);
        }

        private void ReasoningSelectorRowButton_Click(object sender, RoutedEventArgs e)
        {
            SetProfileSelectorSubmenu(modelVisible: false, reasoningVisible: ReasoningSelectorRowButton.IsChecked == true);
        }

        private void ProfileListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source
                || ItemsControl.ContainerFromElement(ProfileListBox, source) is not ListBoxItem)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(CloseProfileSelectorPopup), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ReasoningOptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CopilotReasoningMode mode }
                && DataContext is CopilotChatViewModel viewModel)
            {
                viewModel.SetSelectedProfileReasoningMode(mode);
            }

            CloseProfileSelectorPopup();
        }

        private void CloseSelectorPopupButton_Click(object sender, RoutedEventArgs e)
        {
            CloseProfileSelectorPopup();
        }

        private void SetProfileSelectorSubmenu(bool modelVisible, bool reasoningVisible)
        {
            ModelSelectorRowButton.IsChecked = modelVisible;
            ReasoningSelectorRowButton.IsChecked = reasoningVisible;
            ModelSubmenuBorder.Visibility = modelVisible ? Visibility.Visible : Visibility.Collapsed;
            ReasoningSubmenuBorder.Visibility = reasoningVisible ? Visibility.Visible : Visibility.Collapsed;
            var popupWidth = ProfileSelectorPopupMainWidth + (modelVisible || reasoningVisible ? ProfileSelectorPopupSubmenuWidth : 0);
            ProfileSelectorPopup.HorizontalOffset = ProfileSelectorButton.ActualWidth - popupWidth - ProfileSelectorPopupShadowInset;
        }

        private bool OpenProfileSelector()
        {
            if (DataContext is not CopilotChatViewModel viewModel || !viewModel.CanSelectProfile)
                return false;

            ProfileSelectorButton.IsChecked = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!ProfileSelectorPopup.IsOpen)
                    return;

                SetProfileSelectorSubmenu(modelVisible: true, reasoningVisible: false);
                ProfileListBox.Focus();
                Keyboard.Focus(ProfileListBox);
                if (viewModel.SelectedProfile != null)
                    ProfileListBox.ScrollIntoView(viewModel.SelectedProfile);
            });
            return true;
        }

        private bool OpenReasoningSelector()
        {
            if (DataContext is not CopilotChatViewModel viewModel
                || !viewModel.CanSelectProfile
                || !viewModel.HasConfigurableReasoning)
            {
                return false;
            }

            ProfileSelectorButton.IsChecked = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!ProfileSelectorPopup.IsOpen)
                    return;

                SetProfileSelectorSubmenu(modelVisible: false, reasoningVisible: true);
                ReasoningOptionsControl.UpdateLayout();
                var selectedIndex = ReasoningOptionsControl.Items
                    .Cast<CopilotReasoningOption>()
                    .Select((option, index) => (option, index))
                    .FirstOrDefault(item => item.option.IsSelected)
                    .index;
                var container = ReasoningOptionsControl.ItemContainerGenerator.ContainerFromIndex(selectedIndex);
                var button = container == null ? null : FindVisualChild<Button>(container);
                if (button != null)
                {
                    button.Focus();
                    Keyboard.Focus(button);
                }
                else
                {
                    ReasoningSelectorRowButton.Focus();
                    Keyboard.Focus(ReasoningSelectorRowButton);
                }
            });
            return true;
        }

        private void CloseProfileSelectorPopup()
        {
            if (ProfileSelectorPopup == null)
                return;

            ProfileSelectorPopup.IsOpen = false;
        }
    }
}
