using Conoscope.Core;
using System;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        internal event EventHandler? WindowQuickControlStateChanged;

        internal bool HasActiveViewState => HasDisplayData();
        internal bool CanUseDerivedChannels => HasXyzData();
        internal bool CanUseContrastChannel => HasXyzData() && CanOfferContrastChannel();

        internal void SetWindowQuickDisplayChannel(ExportChannel channel)
        {
            if (RequiresFullXyzData(channel) && !HasXyzData())
            {
                channel = ExportChannel.Y;
            }

            if (channel == ExportChannel.Contrast && !CanOfferContrastChannel())
            {
                channel = ExportChannel.Y;
            }

            if (State.DisplayChannel == channel)
            {
                return;
            }

            State.DisplayChannel = channel;

            if (!HasDisplayData())
            {
                RaiseWindowQuickControlStateChanged();
                return;
            }

            if (channel == ExportChannel.ColorDifference && !CanRefreshColorDifferenceDisplay())
            {
                RaiseWindowQuickControlStateChanged();
                return;
            }

            if (channel == ExportChannel.Contrast && !CanRefreshContrastDisplay())
            {
                RaiseWindowQuickControlStateChanged();
                return;
            }

            try
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
            catch (Exception ex)
            {
                log.Error($"刷新显示通道失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        internal void SetWindowQuickReferenceMode(ConoscopeCoordinateReferenceMode mode)
        {
            ConoscopeCoordinateAxisParam axisParam = State.CoordinateAxis;
            if (axisParam.ReferenceMode == mode)
            {
                return;
            }

            axisParam.ReferenceMode = mode;
            if (coordinateAxisController == null)
            {
                NotifyReferenceStateChanged();
                ApplyCoordinateAxisReference();
            }
        }

        internal void SetWindowQuickContrastImageKind(ContrastReferenceKind kind)
        {
            ApplyContrastImageKind(kind, refreshDisplay: true);
        }

        internal void SaveWindowQuickColorDifferenceReference()
        {
            SaveCurrentAsGlobalColorDifferenceReference();
        }

        internal void SetWindowQuickReferenceValue(double value)
        {
            ConoscopeCoordinateAxisParam axisParam = State.CoordinateAxis;
            if (axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                axisParam.ReferenceAngle = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(value);
            }
            else
            {
                axisParam.ReferenceRadiusAngle = Math.Max(0, Math.Min(value, MaxAngle));
            }

            if (coordinateAxisController == null)
            {
                NotifyReferenceStateChanged();
                ApplyCoordinateAxisReference();
            }
        }

        private void RaiseWindowQuickControlStateChanged()
        {
            WindowQuickControlStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
