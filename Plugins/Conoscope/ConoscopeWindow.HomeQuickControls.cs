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
        private bool isUpdatingActiveViewControls;
        private ConoscopeView? subscribedActiveViewControlView;

        private void RefreshActiveViewControlState(ConoscopeView? activeView)
        {
            AttachActiveViewControlView(activeView);

            if (bdActiveViewControls == null || bdActiveViewExportControls == null)
            {
                return;
            }

            if (activeView == null || !activeView.TryGetWindowQuickControlState(out ConoscopeWindowQuickControlState state))
            {
                bdActiveViewControls.IsEnabled = false;
                bdActiveViewExportControls.IsEnabled = false;

                if (cbActiveContrastImageKind != null)
                {
                    cbActiveContrastImageKind.IsEnabled = false;
                }

                if (btnActiveSaveBlackContrastReference != null)
                {
                    btnActiveSaveBlackContrastReference.IsEnabled = false;
                }

                if (btnActiveSaveWhiteContrastReference != null)
                {
                    btnActiveSaveWhiteContrastReference.IsEnabled = false;
                }

                if (cbActiveColorDifferenceReference != null)
                {
                    cbActiveColorDifferenceReference.IsEnabled = false;
                }

                if (txtActiveColorDifferenceCustomU != null)
                {
                    txtActiveColorDifferenceCustomU.IsEnabled = false;
                }

                if (txtActiveColorDifferenceCustomV != null)
                {
                    txtActiveColorDifferenceCustomV.IsEnabled = false;
                }

                if (btnActiveSaveColorDifferenceReference != null)
                {
                    btnActiveSaveColorDifferenceReference.IsEnabled = false;
                }

                if (panelActiveColorDifferenceCustomUv != null)
                {
                    panelActiveColorDifferenceCustomUv.Visibility = Visibility.Collapsed;
                }

                return;
            }

            bdActiveViewControls.IsEnabled = true;
            bdActiveViewExportControls.IsEnabled = true;

            isUpdatingActiveViewControls = true;
            try
            {
                RefreshActiveViewChannelAvailability(state.CanUseDerivedChannels, state.CanUseContrastChannel);
                ComboBoxHelper.SelectItemByTag(cbActiveDisplayChannel, state.DisplayChannel.ToString());
                ComboBoxHelper.SelectItemByTag(cbActiveContrastImageKind, state.ContrastImageKind.ToString());
                ComboBoxHelper.SelectItemByTag(cbActiveColorDifferenceReference, state.ColorDifferenceReferenceMode.ToString());
                SetActiveReferenceModeSelection(state.ReferenceMode);

                txtActiveReferenceValue.Text = state.ReferenceValue.ToString("F2", CultureInfo.InvariantCulture);
                txtActiveReferenceValue.ToolTip = state.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                    ? Properties.Resources.TipEnterAzimuth
                    : CompositeFormatCache.Format(Properties.Resources.TipEnterPolarAngle, state.ReferenceMaximum);

                txtActiveColorDifferenceCustomU.Text = state.ColorDifferenceCustomU.ToString("F4", CultureInfo.InvariantCulture);
                txtActiveColorDifferenceCustomV.Text = state.ColorDifferenceCustomV.ToString("F4", CultureInfo.InvariantCulture);
                UpdateActiveColorDifferenceCustomVisibility(state.ColorDifferenceReferenceMode);

                cbActiveContrastImageKind.IsEnabled = true;
                btnActiveSaveBlackContrastReference.IsEnabled = true;
                btnActiveSaveWhiteContrastReference.IsEnabled = true;
                cbActiveColorDifferenceReference.IsEnabled = true;
                txtActiveColorDifferenceCustomU.IsEnabled = true;
                txtActiveColorDifferenceCustomV.IsEnabled = true;
                btnActiveSaveColorDifferenceReference.IsEnabled = true;

                UpdateActiveContrastReferenceStatus();
                UpdateActiveColorDifferenceReferenceStatus();
            }
            finally
            {
                isUpdatingActiveViewControls = false;
            }
        }

        private void RefreshActiveViewChannelAvailability(bool canUseDerivedChannels, bool canUseContrastChannel)
        {
            UpdateActiveChannelOptionVisibility(cbActiveDisplayChannel, canUseDerivedChannels, canUseContrastChannel);
        }

        private static void UpdateActiveChannelOptionVisibility(ComboBox? comboBox, bool canUseDerivedChannels, bool canUseContrastChannel)
        {
            if (comboBox == null)
            {
                return;
            }

            foreach (ExportChannel channel in new[]
            {
                ExportChannel.X,
                ExportChannel.Z,
                ExportChannel.CieX,
                ExportChannel.CieY,
                ExportChannel.CieU,
                ExportChannel.CieV,
                ExportChannel.ColorDifference
            })
            {
                ComboBoxHelper.SetItemVisibilityByTag(
                    comboBox,
                    channel.ToString(),
                    canUseDerivedChannels ? Visibility.Visible : Visibility.Collapsed);
            }

            string contrastTag = ExportChannel.Contrast.ToString();
            ComboBoxHelper.SetItemVisibilityByTag(comboBox, contrastTag, canUseContrastChannel ? Visibility.Visible : Visibility.Collapsed);
            bool selectedDerivedChannelUnavailable = !canUseDerivedChannels
                && comboBox.SelectedItem is ComboBoxItem derivedSelectedItem
                && Enum.TryParse<ExportChannel>(derivedSelectedItem.Tag?.ToString(), out ExportChannel derivedChannel)
                && derivedChannel is ExportChannel.X
                    or ExportChannel.Z
                    or ExportChannel.CieX
                    or ExportChannel.CieY
                    or ExportChannel.CieU
                    or ExportChannel.CieV
                    or ExportChannel.ColorDifference;
            bool selectedContrastChannelUnavailable = !canUseContrastChannel
                && comboBox.SelectedItem is ComboBoxItem selectedItem
                && string.Equals(selectedItem.Tag?.ToString(), contrastTag, StringComparison.OrdinalIgnoreCase);

            if (selectedDerivedChannelUnavailable || selectedContrastChannelUnavailable)
            {
                ComboBoxHelper.TrySelectItemByTag(comboBox, ExportChannel.Y.ToString(), visibleOnly: true);
            }
        }

        private void SetActiveReferenceModeSelection(ConoscopeCoordinateReferenceMode mode)
        {
            if (rbActiveReferenceLine != null)
            {
                rbActiveReferenceLine.IsChecked = mode == ConoscopeCoordinateReferenceMode.AzimuthLine;
            }

            if (rbActiveReferenceCircle != null)
            {
                rbActiveReferenceCircle.IsChecked = mode == ConoscopeCoordinateReferenceMode.PolarCircle;
            }
        }

        private void UpdateActiveColorDifferenceCustomVisibility(ColorDifferenceReferenceMode mode)
        {
            if (panelActiveColorDifferenceCustomUv == null)
            {
                return;
            }

            panelActiveColorDifferenceCustomUv.Visibility = mode == ColorDifferenceReferenceMode.Custom ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateActiveContrastReferenceStatus()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.GetInstance().GlobalReferences;
            UpdateActiveContrastReferenceState(
                btnActiveSaveBlackContrastReference,
                globalReferences.HasContrastReference(ContrastReferenceKind.Black),
                ContrastReferenceKind.Black,
                globalReferences.GetContrastReferenceFileName(ContrastReferenceKind.Black));
            UpdateActiveContrastReferenceState(
                btnActiveSaveWhiteContrastReference,
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

        private static void UpdateActiveContrastReferenceState(
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
                    ? CompositeFormatCache.Format(Properties.Resources.TipGlobalContrastReferenceSaved, label, savedName)
                    : CompositeFormatCache.Format(Properties.Resources.TipSaveGlobalContrastReference, label);

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

        private void UpdateActiveColorDifferenceReferenceStatus()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.GetInstance().GlobalReferences;
            bool hasReference = globalReferences.HasColorDifferenceReference;

            if (btnActiveSaveColorDifferenceReference != null)
            {
                string savedName = Path.GetFileName(globalReferences.ColorDifferenceReferenceFileName) ?? Properties.Resources.StateSaved;
                btnActiveSaveColorDifferenceReference.ToolTip = hasReference
                    ? CompositeFormatCache.Format(Properties.Resources.TipGlobalColorDifferenceReferenceSaved, savedName)
                    : Properties.Resources.TipSaveGlobalColorDifferenceReference;

                if (hasReference)
                {
                    btnActiveSaveColorDifferenceReference.Background = Brushes.LightGreen;
                    btnActiveSaveColorDifferenceReference.Foreground = Brushes.Black;
                }
                else
                {
                    btnActiveSaveColorDifferenceReference.ClearValue(Control.BackgroundProperty);
                    btnActiveSaveColorDifferenceReference.ClearValue(Control.ForegroundProperty);
                }
            }
        }

        private void AttachActiveViewControlView(ConoscopeView? activeView)
        {
            if (ReferenceEquals(subscribedActiveViewControlView, activeView))
            {
                return;
            }

            if (subscribedActiveViewControlView != null)
            {
                subscribedActiveViewControlView.WindowQuickControlStateChanged -= ActiveView_WindowQuickControlStateChanged;
            }

            subscribedActiveViewControlView = activeView;

            if (subscribedActiveViewControlView != null)
            {
                subscribedActiveViewControlView.WindowQuickControlStateChanged += ActiveView_WindowQuickControlStateChanged;
            }
        }

        private void DetachActiveViewControlView()
        {
            if (subscribedActiveViewControlView == null)
            {
                return;
            }

            subscribedActiveViewControlView.WindowQuickControlStateChanged -= ActiveView_WindowQuickControlStateChanged;
            subscribedActiveViewControlView = null;
        }

        private void ActiveView_WindowQuickControlStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ReferenceEquals(sender, ActiveView))
                {
                    RefreshActiveViewControlState(ActiveView);
                }
            }));
        }

        private void cbActiveDisplayChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingActiveViewControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ExportChannel channel = ComboBoxHelper.GetSelectedEnumByTag(cbActiveDisplayChannel, ExportChannel.Y);
            ActiveView.SetWindowQuickDisplayChannel(channel);
        }

        private void rbActiveReferenceLine_Checked(object sender, RoutedEventArgs e)
        {
            ApplyActiveReferenceMode(ConoscopeCoordinateReferenceMode.AzimuthLine);
        }

        private void rbActiveReferenceCircle_Checked(object sender, RoutedEventArgs e)
        {
            ApplyActiveReferenceMode(ConoscopeCoordinateReferenceMode.PolarCircle);
        }

        private void ApplyActiveReferenceMode(ConoscopeCoordinateReferenceMode mode)
        {
            if (isUpdatingActiveViewControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ActiveView.SetWindowQuickReferenceMode(mode);
            if (txtActiveReferenceValue != null)
            {
                txtActiveReferenceValue.ToolTip = mode == ConoscopeCoordinateReferenceMode.AzimuthLine
                    ? Properties.Resources.TipEnterAzimuth
                    : CompositeFormatCache.Format(Properties.Resources.TipEnterPolarAngle, ActiveView.MaxAngle);
            }
        }

        private void cbActiveContrastImageKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingActiveViewControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ContrastReferenceKind imageKind = ComboBoxHelper.GetSelectedEnumByTag(cbActiveContrastImageKind, ContrastReferenceKind.Black);
            ActiveView.SetWindowQuickContrastImageKind(imageKind);
        }

        private void cbActiveColorDifferenceReference_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingActiveViewControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ColorDifferenceReferenceMode mode = ComboBoxHelper.GetSelectedEnumByTag(cbActiveColorDifferenceReference, ColorDifferenceReferenceMode.D65);
            UpdateActiveColorDifferenceCustomVisibility(mode);
            ActiveView.SetWindowQuickColorDifferenceReferenceMode(mode);
        }

        private void btnActiveExportAngle_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportAngleMode();
        }

        private void btnActiveExportCircle_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportCircleMode();
        }

        private void btnActiveAdvancedExport_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.AdvancedExport();
        }

        private void btnActiveOpen3D_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.Open3DForCurrentView();
        }

        private void btnActiveOpenCie_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.OpenCieForCurrentView();
        }

        private void btnActiveSaveBlackContrastReference_Click(object sender, RoutedEventArgs e)
        {
            SaveActiveViewContrastReference(ContrastReferenceKind.Black);
        }

        private void btnActiveSaveWhiteContrastReference_Click(object sender, RoutedEventArgs e)
        {
            SaveActiveViewContrastReference(ContrastReferenceKind.White);
        }

        private void btnActiveSaveColorDifferenceReference_Click(object sender, RoutedEventArgs e)
        {
            ToggleColorDifferenceReference();
        }

        private void txtActiveReferenceValue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            ApplyActiveReferenceValueFromText();
            e.Handled = true;
        }

        private void txtActiveReferenceValue_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyActiveReferenceValueFromText();
        }

        private void txtActiveColorDifferenceCustom_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            ApplyActiveColorDifferenceCustomValuesFromText();
            e.Handled = true;
        }

        private void txtActiveColorDifferenceCustom_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyActiveColorDifferenceCustomValuesFromText();
        }

        private void ApplyActiveReferenceValueFromText()
        {
            if (isUpdatingActiveViewControls || ActiveView == null || txtActiveReferenceValue == null)
            {
                return;
            }

            if (!double.TryParse(txtActiveReferenceValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            {
                RefreshActiveViewControlState(ActiveView);
                return;
            }

            if (!ActiveView.TryGetWindowQuickControlState(out _))
            {
                RefreshActiveViewControlState(ActiveView);
                return;
            }

            ActiveView.SetWindowQuickReferenceValue(value);
        }

        private void ApplyActiveColorDifferenceCustomValuesFromText()
        {
            if (isUpdatingActiveViewControls || ActiveView == null || txtActiveColorDifferenceCustomU == null || txtActiveColorDifferenceCustomV == null)
            {
                return;
            }

            if (!double.TryParse(txtActiveColorDifferenceCustomU.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double u)
                || !double.TryParse(txtActiveColorDifferenceCustomV.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                || !double.IsFinite(u)
                || !double.IsFinite(v))
            {
                MessageBox.Show(this, Properties.Resources.MsgInvalidCustomUV, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshActiveViewControlState(ActiveView);
                return;
            }

            ActiveView.SetWindowQuickColorDifferenceCustomReference(u, v);
        }

        private void ToggleColorDifferenceReference()
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.GetInstance().GlobalReferences;
            if (globalReferences.HasColorDifferenceReference)
            {
                globalReferences.ClearColorDifferenceReference();
                ConoscopeModuleService.RefreshAllReferenceState();
                return;
            }

            if (ActiveView == null)
            {
                return;
            }

            try
            {
                ActiveView.SaveWindowQuickColorDifferenceReference();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.GroupColorDifference, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveActiveViewContrastReference(ContrastReferenceKind referenceKind)
        {
            ConoscopeGlobalReferenceStore globalReferences = ConoscopeManager.GetInstance().GlobalReferences;
            if (globalReferences.HasContrastReference(referenceKind))
            {
                globalReferences.ClearContrastReference(referenceKind);
                ConoscopeModuleService.RefreshAllReferenceState();
                return;
            }

            if (ActiveView == null)
            {
                return;
            }

            try
            {
                ActiveView.SaveCurrentAsGlobalContrastReference(referenceKind);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Properties.Resources.GroupContrast, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
