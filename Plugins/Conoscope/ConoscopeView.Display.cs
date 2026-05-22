using ColorVision.ImageEditor;
using ColorVision.UI;
using Conoscope.Presentation.Formatters;
using Conoscope.Presentation.Helpers;
using Conoscope.Core;
using System;
using System.Windows;
using System.Windows.Controls;

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
            if (!HasXyzData())
            {
                UpdatePseudoColorLegendVisibility(false);
                RaiseWindowQuickControlStateChanged();
                return;
            }

            ExportChannel displayChannel = GetSelectedDisplayChannel();
            OpenCvSharp.Mat? rangeMask = GetPseudoColorRangeMask(XMat!.Width, XMat.Height);
            ConoscopePseudoColorRenderResult renderResult = ConoscopePseudoColorRenderer.Render(
                XMat,
                YMat!,
                ZMat!,
                displayChannel,
                RenderingConfig.PseudoColorMap,
                CreateColorDifferenceMat,
                CreateContrastMat,
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

        private bool CanOfferContrastChannel()
        {
            return GlobalReferences.HasContrastReference(GetRequiredContrastReferenceKind());
        }

        private void RefreshChannelAvailability()
        {
            bool canOfferContrastChannel = CanOfferContrastChannel();
            UpdateChannelOptionVisibility(cbDisplayChannel, ExportChannel.Contrast, canOfferContrastChannel);
            UpdateChannelOptionVisibility(cbExportChannel, ExportChannel.Contrast, canOfferContrastChannel);

            if (!canOfferContrastChannel && RenderingConfig.DisplayChannel == ExportChannel.Contrast)
            {
                RenderingConfig.DisplayChannel = ExportChannel.Y;
            }

            if (!canOfferContrastChannel && GetSelectedExportChannel() == ExportChannel.Contrast)
            {
                ComboBoxHelper.TrySelectItemByTag(cbExportChannel, ExportChannel.Y.ToString(), visibleOnly: true);
            }
        }

        private static void UpdateChannelOptionVisibility(ComboBox? comboBox, ExportChannel channel, bool isVisible)
        {
            if (comboBox == null)
            {
                return;
            }

            string tag = channel.ToString();
            ComboBoxHelper.SetItemVisibilityByTag(comboBox, tag, isVisible ? Visibility.Visible : Visibility.Collapsed);
            if (!isVisible
                && comboBox.SelectedItem is ComboBoxItem selectedItem
                && string.Equals(selectedItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                ComboBoxHelper.TrySelectItemByTag(comboBox, ExportChannel.Y.ToString(), visibleOnly: true);
            }
        }

        private ExportChannel GetSelectedDisplayChannel()
        {
            return ComboBoxHelper.GetSelectedEnumByTag(cbDisplayChannel, RenderingConfig.DisplayChannel);
        }

        private void DisplayChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingDisplayControls)
            {
                return;
            }

            ExportChannel channel = GetSelectedDisplayChannel();
            if (channel == ExportChannel.Contrast && !CanOfferContrastChannel())
            {
                RefreshChannelAvailability();
                RaiseWindowQuickControlStateChanged();
                return;
            }

            RenderingConfig.DisplayChannel = channel;
            UpdateColorDifferencePanelVisibility();

            if (HasXyzData())
            {
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
                    MessageBox.Show(ex.Message, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ExportChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshChannelAvailability();
            UpdateColorDifferencePanelVisibility();
            UpdateContrastReferenceUi();
            RaiseWindowQuickControlStateChanged();
        }

        private void btnSaveConoscopeConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
                MessageBox.Show(Properties.Resources.MsgConfigSaved, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"保存 Conoscope 配置失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgSaveConfigFailedDetail, ex.Message), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
