using ColorVision.ImageEditor;
using ColorVision.UI;
using Conoscope.Presentation.Formatters;
using Conoscope.Core;
using System;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private OpenCvSharp.Mat? pseudoColorRangeMask;
        private OpenCvSharp.Mat? pseudoColorRangeOutsideMask;
        private int pseudoColorRangeMaskWidth;
        private int pseudoColorRangeMaskHeight;
        private int pseudoColorRangeMaskCenterX;
        private int pseudoColorRangeMaskCenterY;
        private int pseudoColorRangeMaskRadius;

        private void RefreshDisplayedImage()
        {
            if (!HasDisplayData())
            {
                UpdatePseudoColorLegendVisibility(false);
                RaiseWindowQuickControlStateChanged();
                return;
            }

            ExportChannel displayChannel = GetSelectedDisplayChannel();
            OpenCvSharp.Mat displayBaseMat = YMat!;
            OpenCvSharp.Mat? rangeMask = GetPseudoColorRangeMask(displayBaseMat.Width, displayBaseMat.Height);
            ConoscopePseudoColorRenderResult renderResult = ConoscopePseudoColorRenderer.Render(
                XMat ?? displayBaseMat,
                YMat!,
                ZMat ?? displayBaseMat,
                displayChannel,
                RenderingConfig.PseudoColorMap,
                () => CreateColorDifferenceMat() ?? displayBaseMat,
                () => CreateContrastMat() ?? displayBaseMat,
                RenderingConfig.UsePseudoColor,
                rangeMask,
                rangeMask == null ? null : pseudoColorRangeOutsideMask);

            UpdateReferenceScale(renderResult.Channel, renderResult.MaxValue);
            if (RenderingConfig.UsePseudoColor)
            {
                UpdatePseudoColorLegend(renderResult.Channel, renderResult.MinValue, renderResult.MaxValue);
            }
            else
            {
                UpdatePseudoColorLegendVisibility(false);
            }

            DisposeCoordinateAxis();
            ImageView.Clear();
            ImageView.SetImageSource(renderResult.Bitmap);
            CreateAndAnalyzePolarLines();
            ApplyZoomAfterDisplayRefresh();
            RaiseWindowQuickControlStateChanged();
        }

        private void UpdatePseudoColorLegend(ExportChannel channel, double minValue, double maxValue)
        {
            UpdateReferenceScale(channel, maxValue);

            if (tbPseudoColorLegendTitle == null || tbPseudoColorLegendMin == null || tbPseudoColorLegendMax == null)
            {
                return;
            }

            UpdatePseudoColorMapPreview();
            tbPseudoColorLegendTitle.Text = ConoscopeChannelDisplayFormatter.GetLabel(channel);
            tbPseudoColorLegendMin.Text = ConoscopeChannelDisplayFormatter.FormatValue(minValue, channel);
            tbPseudoColorLegendMax.Text = ConoscopeChannelDisplayFormatter.FormatValue(maxValue, channel);
            UpdatePseudoColorLegendVisibility(true);
        }

        private void UpdateReferenceScale(ExportChannel channel, double maxValue)
        {
            currentReferenceScaleChannel = channel;
            currentReferenceScaleMaximum = maxValue;
        }

        private OpenCvSharp.Mat? GetPseudoColorRangeMask(int imageWidth, int imageHeight)
        {
            if (!RenderingConfig.UsePseudoColorRangeLimit)
            {
                return null;
            }

            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return null;
            }

            double pixelsPerDegree = CurrentModelProfile.GetConoscopeCoefficient(imageWidth, imageHeight);
            double radiusValue = MaxAngle * pixelsPerDegree;
            if (!double.IsFinite(radiusValue) || radiusValue <= 0)
            {
                return null;
            }

            int centerX = (int)Math.Round(imageWidth / 2.0);
            int centerY = (int)Math.Round(imageHeight / 2.0);
            int radius = Math.Max(1, (int)Math.Round(radiusValue));

            if (pseudoColorRangeMask != null
                && pseudoColorRangeOutsideMask != null
                && pseudoColorRangeMaskWidth == imageWidth
                && pseudoColorRangeMaskHeight == imageHeight
                && pseudoColorRangeMaskCenterX == centerX
                && pseudoColorRangeMaskCenterY == centerY
                && pseudoColorRangeMaskRadius == radius)
            {
                return pseudoColorRangeMask;
            }

            DisposePseudoColorRangeMasks();

            pseudoColorRangeMaskWidth = imageWidth;
            pseudoColorRangeMaskHeight = imageHeight;
            pseudoColorRangeMaskCenterX = centerX;
            pseudoColorRangeMaskCenterY = centerY;
            pseudoColorRangeMaskRadius = radius;

            pseudoColorRangeMask = new OpenCvSharp.Mat(imageHeight, imageWidth, OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(0));
            OpenCvSharp.Cv2.Circle(
                pseudoColorRangeMask,
                new OpenCvSharp.Point(centerX, centerY),
                radius,
                OpenCvSharp.Scalar.All(255),
                -1,
                OpenCvSharp.LineTypes.Link8);

            pseudoColorRangeOutsideMask = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.BitwiseNot(pseudoColorRangeMask, pseudoColorRangeOutsideMask);
            return pseudoColorRangeMask;
        }

        private void DisposePseudoColorRangeMasks()
        {
            pseudoColorRangeMask?.Dispose();
            pseudoColorRangeMask = null;
            pseudoColorRangeOutsideMask?.Dispose();
            pseudoColorRangeOutsideMask = null;
            pseudoColorRangeMaskWidth = 0;
            pseudoColorRangeMaskHeight = 0;
            pseudoColorRangeMaskCenterX = 0;
            pseudoColorRangeMaskCenterY = 0;
            pseudoColorRangeMaskRadius = 0;
        }

        private void UpdatePseudoColorMapPreview()
        {
            if (imgPseudoColorLegend == null)
            {
                return;
            }

            imgPseudoColorLegend.Source = ColormapConstats.CreatePreviewImage(RenderingConfig.PseudoColorMap);
        }

        private void UpdatePseudoColorLegendVisibility(bool isVisible)
        {
            if (PseudoColorLegendPanel == null)
            {
                return;
            }

            PseudoColorLegendPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool HasXyzData()
        {
            return XMat != null && YMat != null && ZMat != null;
        }

        private bool HasDisplayData()
        {
            return YMat != null;
        }

        private static bool RequiresFullXyzData(ExportChannel channel)
        {
            return channel is ExportChannel.X
                or ExportChannel.Z
                or ExportChannel.CieX
                or ExportChannel.CieY
                or ExportChannel.CieU
                or ExportChannel.CieV
                or ExportChannel.ColorDifference
                or ExportChannel.Contrast;
        }

        private bool CanOfferContrastChannel()
        {
            return GlobalReferences.HasContrastReference(GetRequiredContrastReferenceKind());
        }

        private void RefreshChannelAvailability()
        {
            bool hasFullXyzData = HasXyzData();
            bool canOfferContrastChannel = hasFullXyzData && CanOfferContrastChannel();

            if (RequiresFullXyzData(RenderingConfig.DisplayChannel) && !hasFullXyzData)
            {
                RenderingConfig.DisplayChannel = ExportChannel.Y;
            }

            if (!canOfferContrastChannel && RenderingConfig.DisplayChannel == ExportChannel.Contrast)
            {
                RenderingConfig.DisplayChannel = ExportChannel.Y;
            }

            if ((RequiresFullXyzData(State.ExportChannel) && !hasFullXyzData)
                || (!canOfferContrastChannel && State.ExportChannel == ExportChannel.Contrast))
            {
                State.ExportChannel = ExportChannel.Y;
            }
        }

        private ExportChannel GetSelectedDisplayChannel() => State.DisplayChannel;

        private void btnSaveConoscopeConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
                MessageBox.Show(Properties.Resources.MsgConfigSaved, Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"保存 Conoscope 配置失败: {ex.Message}", ex);
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgSaveConfigFailedDetail, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
