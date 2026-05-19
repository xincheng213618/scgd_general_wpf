using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using System;
using System.Globalization;
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

            if (bdHomeQuickControls == null)
            {
                return;
            }

            if (activeView == null || !activeView.TryGetWindowQuickControlState(out ConoscopeWindowQuickControlState state))
            {
                bdHomeQuickControls.Visibility = Visibility.Collapsed;
                return;
            }

            bdHomeQuickControls.Visibility = Visibility.Visible;

            isUpdatingHomeQuickControls = true;
            try
            {
                ComboBoxHelper.SelectItemByTag(cbHomeDisplayChannel, state.DisplayChannel.ToString());
                ComboBoxHelper.SelectItemByTag(cbHomeExportChannel, state.ExportChannel.ToString());
                ComboBoxHelper.SelectItemByTag(cbHomeReferenceMode, state.ReferenceMode.ToString());
                tbHomeReferenceValueLabel.Text = state.ReferenceLabel;
                txtHomeReferenceValue.Text = state.ReferenceValue.ToString("F2", CultureInfo.InvariantCulture);
                txtHomeReferenceValue.ToolTip = state.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                    ? Properties.Resources.TipEnterAzimuth
                    : string.Format(Properties.Resources.TipEnterPolarAngle, state.ReferenceMaximum);
            }
            finally
            {
                isUpdatingHomeQuickControls = false;
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

        private void cbHomeReferenceMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingHomeQuickControls || !IsInitialized || ActiveView == null)
            {
                return;
            }

            ConoscopeCoordinateReferenceMode mode = ComboBoxHelper.GetSelectedEnumByTag(cbHomeReferenceMode, ConoscopeCoordinateReferenceMode.AzimuthLine);
            ActiveView.SetWindowQuickReferenceMode(mode);
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

        private void btnHomeExportAngle_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportAngleMode();
        }

        private void btnHomeExportCircle_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportCircleMode();
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
    }
}