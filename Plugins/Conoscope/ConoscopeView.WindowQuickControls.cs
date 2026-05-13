using Conoscope.Core;
using System;
using System.Windows;

namespace Conoscope
{
    public readonly record struct ConoscopeWindowQuickControlState(
        ExportChannel DisplayChannel,
        ConoscopeCoordinateReferenceMode ReferenceMode,
        double ReferenceValue,
        double ReferenceMaximum)
    {
        public string ReferenceLabel => ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine ? "方位角(°)" : "极角(°)";
    }

    public partial class ConoscopeView
    {
        public event EventHandler? WindowQuickControlStateChanged;

        public bool TryGetWindowQuickControlState(out ConoscopeWindowQuickControlState state)
        {
            if (!HasXyzData())
            {
                state = default;
                return false;
            }

            ConoscopeCoordinateAxisParam axisParam = CurrentModelProfile.CoordinateAxisParam;
            double referenceValue = axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? axisParam.ReferenceAngle
                : axisParam.ReferenceRadiusAngle;
            double referenceMaximum = axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine ? 180.0 : MaxAngle;

            state = new ConoscopeWindowQuickControlState(
                RenderingConfig.DisplayChannel,
                axisParam.ReferenceMode,
                referenceValue,
                referenceMaximum);
            return true;
        }

        public void SetWindowQuickDisplayChannel(ExportChannel channel)
        {
            if (RenderingConfig.DisplayChannel == channel)
            {
                return;
            }

            RenderingConfig.DisplayChannel = channel;
            RefreshDisplayControlsFromConfig();

            if (!HasXyzData())
            {
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
                MessageBox.Show(ex.Message, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void SetWindowQuickReferenceMode(ConoscopeCoordinateReferenceMode mode)
        {
            ConoscopeCoordinateAxisParam axisParam = CurrentModelProfile.CoordinateAxisParam;
            if (axisParam.ReferenceMode == mode)
            {
                return;
            }

            axisParam.ReferenceMode = mode;
            RefreshQuickControlsFromAxisParam();
            ApplyCoordinateAxisReference();
        }

        public void SetWindowQuickReferenceValue(double value)
        {
            ConoscopeCoordinateAxisParam axisParam = CurrentModelProfile.CoordinateAxisParam;
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