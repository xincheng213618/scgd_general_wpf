using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private bool isUpdatingHomeQuickControls;
        private ConoscopeView? subscribedHomeQuickControlView;

        private void RefreshHomeQuickControlState(ConoscopeView? activeView)
        {
            AttachHomeQuickControlView(activeView);

            if (bdHomeQuickControls == null || bdHomeExportControls == null)
            {
                return;
            }

            if (activeView == null || !activeView.TryGetWindowQuickControlState(out ConoscopeWindowQuickControlState state))
            {
                bdHomeQuickControls.Visibility = Visibility.Collapsed;
                bdHomeExportControls.Visibility = Visibility.Collapsed;
                return;
            }

            bdHomeQuickControls.Visibility = Visibility.Visible;
            bdHomeExportControls.Visibility = Visibility.Visible;

            isUpdatingHomeQuickControls = true;
            try
            {
                RefreshHomeChannelAvailability(state.CanUseContrastChannel);
                ComboBoxHelper.SelectItemByTag(cbHomeDisplayChannel, state.DisplayChannel.ToString());
                ComboBoxHelper.SelectItemByTag(cbHomeExportChannel, state.ExportChannel.ToString());
                ComboBoxHelper.SelectItemByTag(cbHomeContrastImageKind, state.ContrastImageKind.ToString());
                ComboBoxHelper.SelectItemByTag(cbHomeColorDifferenceReference, state.ColorDifferenceReferenceMode.ToString());
                SetHomeReferenceModeSelection(state.ReferenceMode);

                txtHomeReferenceValue.Text = state.ReferenceValue.ToString("F2", CultureInfo.InvariantCulture);
                txtHomeReferenceValue.ToolTip = state.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                    ? Properties.Resources.TipEnterAzimuth
                    : string.Format(Properties.Resources.TipEnterPolarAngle, state.ReferenceMaximum);

                txtHomeColorDifferenceCustomU.Text = state.ColorDifferenceCustomU.ToString("F4", CultureInfo.InvariantCulture);
                txtHomeColorDifferenceCustomV.Text = state.ColorDifferenceCustomV.ToString("F4", CultureInfo.InvariantCulture);
                UpdateHomeColorDifferenceCustomVisibility(state.ColorDifferenceReferenceMode);

                btnHomeSaveBlackContrastReference.IsEnabled = true;
                btnHomeSaveWhiteContrastReference.IsEnabled = true;
                btnHomeSaveColorDifferenceReference.IsEnabled = true;

                UpdateHomeContrastReferenceStatus();
                UpdateHomeColorDifferenceReferenceStatus();
            }
            finally
            {
                isUpdatingHomeQuickControls = false;
            }
        }

        private void RefreshHomeChannelAvailability(bool canUseContrastChannel)
        {
            UpdateHomeChannelOptionVisibility(cbHomeDisplayChannel, canUseContrastChannel);
            UpdateHomeChannelOptionVisibility(cbHomeExportChannel, canUseContrastChannel);
        }

        private static void UpdateHomeChannelOptionVisibility(ComboBox? comboBox, bool canUseContrastChannel)
        {
            if (comboBox == null)
            {
                return;
            }

            string contrastTag = ExportChannel.Contrast.ToString();
            ComboBoxHelper.SetItemVisibilityByTag(comboBox, contrastTag, canUseContrastChannel ? Visibility.Visible : Visibility.Collapsed);
            if (!canUseContrastChannel
                && comboBox.SelectedItem is ComboBoxItem selectedItem
                && string.Equals(selectedItem.Tag?.ToString(), contrastTag, StringComparison.OrdinalIgnoreCase))
            {
                ComboBoxHelper.TrySelectItemByTag(comboBox, ExportChannel.Y.ToString(), visibleOnly: true);
            }
        }

        private void SetHomeReferenceModeSelection(ConoscopeCoordinateReferenceMode mode)
        {
            if (rbHomeReferenceLine != null)
            {
                rbHomeReferenceLine.IsChecked = mode == ConoscopeCoordinateReferenceMode.AzimuthLine;
            }

            if (rbHomeReferenceCircle != null)
            {
                rbHomeReferenceCircle.IsChecked = mode == ConoscopeCoordinateReferenceMode.PolarCircle;
            }
        }

        private void UpdateHomeColorDifferenceCustomVisibility(ColorDifferenceReferenceMode mode)
        {
            if (panelHomeColorDifferenceCustomUv == null)
            {
                return;
            }

            panelHomeColorDifferenceCustomUv.Visibility = mode == ColorDifferenceReferenceMode.Custom ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateHomeContrastReferenceStatus()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.GetInstance().GlobalReferences;
            UpdateHomeContrastReferenceState(
                btnHomeSaveBlackContrastReference,
                globalReferences.HasContrastReference(ContrastReferenceKind.Black),
                ContrastReferenceKind.Black,
                globalReferences.GetContrastReferenceFileName(ContrastReferenceKind.Black));
            UpdateHomeContrastReferenceState(
                btnHomeSaveWhiteContrastReference,
                globalReferences.HasContrastReference(ContrastReferenceKind.White),
                ContrastReferenceKind.White,
                globalReferences.GetContrastReferenceFileName(ContrastReferenceKind.White));
        }

        private static string GetContrastReferenceLabel(ContrastReferenceKind referenceKind)
        {
            return referenceKind == ContrastReferenceKind.Black
                ? Properties.Resources.ContrastReferenceBlackField
                : Properties.Resources.ContrastReferenceWhiteField;
        }

        private static void UpdateHomeContrastReferenceState(
            Button? button,
            bool isSaved,
            ContrastReferenceKind referenceKind,
            string? fileName)
        {
            if (button != null)
            {
                string label = GetContrastReferenceLabel(referenceKind);
                string savedName = Path.GetFileName(fileName) ?? Properties.Resources.StateSaved;
                button.ToolTip = isSaved
                    ? string.Format(Properties.Resources.TipGlobalContrastReferenceSaved, label, savedName)
                    : string.Format(Properties.Resources.TipSaveGlobalContrastReference, label);

                if (isSaved)
                {
                    button.Background = Brushes.LightGreen;
                    button.Foreground = Brushes.Black;
                }
                else
                {
                    button.ClearValue(Control.BackgroundProperty);
                    button.ClearValue(Control.ForegroundProperty);
                }
            }
        }

        private void UpdateHomeColorDifferenceReferenceStatus()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.GetInstance().GlobalReferences;
            bool hasReference = globalReferences.HasColorDifferenceReference;

            if (btnHomeSaveColorDifferenceReference != null)
            {
                string savedName = Path.GetFileName(globalReferences.ColorDifferenceReferenceFileName) ?? Properties.Resources.StateSaved;
                btnHomeSaveColorDifferenceReference.ToolTip = hasReference
                    ? string.Format(Properties.Resources.TipGlobalColorDifferenceReferenceSaved, savedName)
                    : Properties.Resources.TipSaveGlobalColorDifferenceReference;

                if (hasReference)
                {
                    btnHomeSaveColorDifferenceReference.Background = Brushes.LightGreen;
                    btnHomeSaveColorDifferenceReference.Foreground = Brushes.Black;
                }
                else
                {
                    btnHomeSaveColorDifferenceReference.ClearValue(Control.BackgroundProperty);
                    btnHomeSaveColorDifferenceReference.ClearValue(Control.ForegroundProperty);
                }
            }
        }

        private void AttachHomeQuickControlView(ConoscopeView? activeView)
        {
            if (ReferenceEquals(subscribedHomeQuickControlView, activeView))
            {
                return;
            }

            if (subscribedHomeQuickControlView != null)
            {
                subscribedHomeQuickControlView.WindowQuickControlStateChanged -= ActiveView_WindowQuickControlStateChanged;
            }

            subscribedHomeQuickControlView = activeView;

            if (subscribedHomeQuickControlView != null)
            {
                subscribedHomeQuickControlView.WindowQuickControlStateChanged += ActiveView_WindowQuickControlStateChanged;
            }
        }

        private void DetachHomeQuickControlView()
        {
            if (subscribedHomeQuickControlView == null)
            {
                return;
            }

            subscribedHomeQuickControlView.WindowQuickControlStateChanged -= ActiveView_WindowQuickControlStateChanged;
            subscribedHomeQuickControlView = null;
        }

        private void ActiveView_WindowQuickControlStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ReferenceEquals(sender, ActiveView))
                {
                    RefreshHomeQuickControlState(ActiveView);
                }
            }));
        }

        private void cbHomeDisplayChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingHomeQuickControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ExportChannel channel = ComboBoxHelper.GetSelectedEnumByTag(cbHomeDisplayChannel, ExportChannel.Y);
            ActiveView.SetWindowQuickDisplayChannel(channel);
            SetOperationStatus(string.Format(Properties.Resources.MsgChannelSwitched, channel), Brushes.LimeGreen);
        }

        private void rbHomeReferenceLine_Checked(object sender, RoutedEventArgs e)
        {
            ApplyHomeReferenceMode(ConoscopeCoordinateReferenceMode.AzimuthLine);
        }

        private void rbHomeReferenceCircle_Checked(object sender, RoutedEventArgs e)
        {
            ApplyHomeReferenceMode(ConoscopeCoordinateReferenceMode.PolarCircle);
        }

        private void ApplyHomeReferenceMode(ConoscopeCoordinateReferenceMode mode)
        {
            if (isUpdatingHomeQuickControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ActiveView.SetWindowQuickReferenceMode(mode);
            txtHomeReferenceValue.ToolTip = mode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? Properties.Resources.TipEnterAzimuth
                : string.Format(Properties.Resources.TipEnterPolarAngle, ActiveView.MaxAngle);
            SetOperationStatus(mode == ConoscopeCoordinateReferenceMode.AzimuthLine ? Properties.Resources.MsgRefModeSwitchedAzimuth : Properties.Resources.MsgRefModeSwitchedPolar, Brushes.LimeGreen);
        }

        private void cbHomeExportChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingHomeQuickControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ExportChannel channel = ComboBoxHelper.GetSelectedEnumByTag(cbHomeExportChannel, ExportChannel.Y);
            ActiveView.SetWindowQuickExportChannel(channel);
            SetOperationStatus(string.Format(Properties.Resources.MsgExportChannelSwitched, channel), Brushes.LimeGreen);
        }

        private void cbHomeContrastImageKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingHomeQuickControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ContrastReferenceKind imageKind = ComboBoxHelper.GetSelectedEnumByTag(cbHomeContrastImageKind, ContrastReferenceKind.Black);
            ActiveView.SetWindowQuickContrastImageKind(imageKind);
            string imageLabel = imageKind == ContrastReferenceKind.Black
                ? Properties.Resources.ContrastImageBlack
                : Properties.Resources.ContrastImageWhite;
            SetOperationStatus(string.Format(Properties.Resources.MsgCurrentImageSwitched, imageLabel), Brushes.LimeGreen);
        }

        private void cbHomeColorDifferenceReference_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingHomeQuickControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ColorDifferenceReferenceMode mode = ComboBoxHelper.GetSelectedEnumByTag(cbHomeColorDifferenceReference, ColorDifferenceReferenceMode.D65);
            UpdateHomeColorDifferenceCustomVisibility(mode);
            ActiveView.SetWindowQuickColorDifferenceReferenceMode(mode);
            SetOperationStatus(string.Format(Properties.Resources.MsgColorDifferenceReferenceSwitched, GetSelectedComboBoxText(cbHomeColorDifferenceReference)), Brushes.LimeGreen);
        }

        private static string GetSelectedComboBoxText(ComboBox? comboBox)
        {
            return comboBox?.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? string.Empty
                : string.Empty;
        }

        private void btnHomeExportAngle_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportAngleMode();
        }

        private void btnHomeExportCircle_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportCircleMode();
        }

        private void btnHomeSaveBlackContrastReference_Click(object sender, RoutedEventArgs e)
        {
            SaveActiveViewContrastReference(ContrastReferenceKind.Black);
        }

        private void btnHomeSaveWhiteContrastReference_Click(object sender, RoutedEventArgs e)
        {
            SaveActiveViewContrastReference(ContrastReferenceKind.White);
        }

        private void btnHomeSaveColorDifferenceReference_Click(object sender, RoutedEventArgs e)
        {
            ToggleColorDifferenceReference();
        }

        private void txtHomeReferenceValue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            ApplyHomeReferenceValueFromText();
            e.Handled = true;
        }

        private void txtHomeReferenceValue_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyHomeReferenceValueFromText();
        }

        private void txtHomeColorDifferenceCustom_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            ApplyHomeColorDifferenceCustomValuesFromText();
            e.Handled = true;
        }

        private void txtHomeColorDifferenceCustom_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyHomeColorDifferenceCustomValuesFromText();
        }

        private void ApplyHomeReferenceValueFromText()
        {
            if (isUpdatingHomeQuickControls || ActiveView == null || txtHomeReferenceValue == null)
            {
                return;
            }

            if (!double.TryParse(txtHomeReferenceValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            {
                RefreshHomeQuickControlState(ActiveView);
                return;
            }

            if (!ActiveView.TryGetWindowQuickControlState(out ConoscopeWindowQuickControlState state))
            {
                RefreshHomeQuickControlState(ActiveView);
                return;
            }

            ActiveView.SetWindowQuickReferenceValue(value);
            SetOperationStatus(state.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? string.Format(Properties.Resources.MsgRefAzimuthSet, value)
                : string.Format(Properties.Resources.MsgRefPolarSet, value), Brushes.LimeGreen);
        }

        private void ApplyHomeColorDifferenceCustomValuesFromText()
        {
            if (isUpdatingHomeQuickControls || ActiveView == null || txtHomeColorDifferenceCustomU == null || txtHomeColorDifferenceCustomV == null)
            {
                return;
            }

            if (!double.TryParse(txtHomeColorDifferenceCustomU.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double u)
                || !double.TryParse(txtHomeColorDifferenceCustomV.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                || !double.IsFinite(u)
                || !double.IsFinite(v))
            {
                MessageBox.Show(this, Properties.Resources.MsgInvalidCustomUV, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshHomeQuickControlState(ActiveView);
                return;
            }

            ActiveView.SetWindowQuickColorDifferenceCustomReference(u, v);
            SetOperationStatus(string.Format(CultureInfo.CurrentCulture, Properties.Resources.MsgColorDifferenceCustomReferenceUpdated, u, v), Brushes.LimeGreen);
        }

        private void ToggleColorDifferenceReference()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.GetInstance().GlobalReferences;
            if (globalReferences.HasColorDifferenceReference)
            {
                globalReferences.ClearColorDifferenceReference();
                ConoscopeModuleService.RefreshAllReferenceState();
                SetOperationStatus(Properties.Resources.MsgGlobalColorDifferenceReferenceCleared, Brushes.OrangeRed);
                return;
            }

            if (ActiveView == null)
            {
                return;
            }

            try
            {
                ActiveView.SaveWindowQuickColorDifferenceReference();
                SetOperationStatus(Properties.Resources.MsgGlobalColorDifferenceReferenceSaved, Brushes.LimeGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.GroupColorDifference, MessageBoxButton.OK, MessageBoxImage.Warning);
                SetOperationStatus(Properties.Resources.MsgSaveGlobalColorDifferenceReferenceFailed, Brushes.OrangeRed);
            }
        }

        private void SaveActiveViewContrastReference(ContrastReferenceKind referenceKind)
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.GetInstance().GlobalReferences;
            if (globalReferences.HasContrastReference(referenceKind))
            {
                globalReferences.ClearContrastReference(referenceKind);
                ConoscopeModuleService.RefreshAllReferenceState();
                SetOperationStatus(string.Format(Properties.Resources.MsgGlobalContrastReferenceCleared, GetContrastReferenceLabel(referenceKind)), Brushes.OrangeRed);
                return;
            }

            if (ActiveView == null)
            {
                return;
            }

            try
            {
                ActiveView.SaveCurrentAsGlobalContrastReference(referenceKind);
                SetOperationStatus(string.Format(Properties.Resources.MsgGlobalContrastReferenceSaved, GetContrastReferenceLabel(referenceKind)), Brushes.LimeGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.GroupContrast, MessageBoxButton.OK, MessageBoxImage.Warning);
                SetOperationStatus(Properties.Resources.MsgSaveGlobalContrastReferenceFailed, Brushes.OrangeRed);
            }
        }
    }
}