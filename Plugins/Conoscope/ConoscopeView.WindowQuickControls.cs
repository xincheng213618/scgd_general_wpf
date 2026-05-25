using Conoscope.Core;
using System;
using System.Windows;

namespace Conoscope
{
    public readonly record struct ConoscopeWindowQuickControlState(
        ExportChannel DisplayChannel,
        ExportChannel ExportChannel,
        ConoscopeCoordinateReferenceMode ReferenceMode,
        double ReferenceValue,
        double ReferenceMaximum,
        ContrastReferenceKind ContrastImageKind,
        ColorDifferenceReferenceMode ColorDifferenceReferenceMode,
        double ColorDifferenceCustomU,
        double ColorDifferenceCustomV,
        bool CanUseDerivedChannels,
        bool CanUseContrastChannel)
    {
        public string ReferenceLabel => ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine ? Properties.Resources.LabelAzimuthDegLabel : Properties.Resources.LabelPolarDegLabel;
    }

    public partial class ConoscopeView
    {
        public event EventHandler? WindowQuickControlStateChanged;

        public bool TryGetWindowQuickControlState(out ConoscopeWindowQuickControlState state)
        {
            if (!HasDisplayData())
            {
                state = default;
                return false;
            }

            ConoscopeCoordinateAxisParam axisParam = CoordinateAxisConfig;
            double referenceValue = axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? axisParam.ReferenceAngle
                : axisParam.ReferenceRadiusAngle;
            double referenceMaximum = axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine ? 180.0 : MaxAngle;

            state = new ConoscopeWindowQuickControlState(
                RenderingConfig.DisplayChannel,
                GetSelectedExportChannel(),
                axisParam.ReferenceMode,
                referenceValue,
                referenceMaximum,
                GetCurrentContrastImageKind(),
                GetSelectedColorDifferenceReferenceMode(),
                ColorDifferenceConfig.CustomU,
                ColorDifferenceConfig.CustomV,
                HasXyzData(),
                HasXyzData() && CanOfferContrastChannel());
            return true;
        }

        public void SetWindowQuickDisplayChannel(ExportChannel channel)
        {
            if (RequiresFullXyzData(channel) && !HasXyzData())
            {
                channel = ExportChannel.Y;
            }

            if (channel == ExportChannel.Contrast && !CanOfferContrastChannel())
            {
                channel = ExportChannel.Y;
            }

            if (RenderingConfig.DisplayChannel == channel)
            {
                return;
            }

            RenderingConfig.DisplayChannel = channel;
            RefreshDisplayControlsFromConfig();

            if (!HasDisplayData())
            {
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

        public void SetWindowQuickReferenceMode(ConoscopeCoordinateReferenceMode mode)
        {
            ConoscopeCoordinateAxisParam axisParam = CoordinateAxisConfig;
            if (axisParam.ReferenceMode == mode)
            {
                return;
            }

            axisParam.ReferenceMode = mode;
            RefreshQuickControlsFromAxisParam();
            ApplyCoordinateAxisReference();
        }

        public void SetWindowQuickExportChannel(ExportChannel channel)
        {
            if (RequiresFullXyzData(channel) && !HasXyzData())
            {
                channel = ExportChannel.Y;
            }

            if (channel == ExportChannel.Contrast && !CanOfferContrastChannel())
            {
                channel = ExportChannel.Y;
            }

            if (GetSelectedExportChannel() == channel)
            {
                return;
            }

            selectedExportChannel = channel;
            RaiseWindowQuickControlStateChanged();
        }

        public void SetWindowQuickContrastImageKind(ContrastReferenceKind kind)
        {
            ApplyContrastImageKind(kind, refreshDisplay: true);
        }

        public void SaveWindowQuickColorDifferenceReference()
        {
            SaveCurrentAsGlobalColorDifferenceReference();
        }

        public void SetWindowQuickReferenceValue(double value)
        {
            ConoscopeCoordinateAxisParam axisParam = CoordinateAxisConfig;
            if (axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                axisParam.ReferenceAngle = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(value);
            }
            else
            {
                axisParam.ReferenceRadiusAngle = Math.Max(0, Math.Min(value, MaxAngle));
            }

            RefreshQuickControlsFromAxisParam();
            ApplyCoordinateAxisReference();
        }

        private void RaiseWindowQuickControlStateChanged()
        {
            WindowQuickControlStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
