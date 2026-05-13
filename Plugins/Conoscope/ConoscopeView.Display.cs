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
        private void RefreshDisplayedImage()
        {
            if (!HasXyzData())
            {
                UpdatePseudoColorLegendVisibility(false);
                return;
            }

            ExportChannel displayChannel = GetSelectedDisplayChannel();
            ConoscopePseudoColorRenderResult renderResult = ConoscopePseudoColorRenderer.Render(
                XMat!,
                YMat!,
                ZMat!,
                displayChannel,
                RenderingConfig.PseudoColorMap,
                CreateColorDifferenceMat,
                RenderingConfig.UsePseudoColor);

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
            ImageView.UpdateZoomAndScale();

            CreateAndAnalyzePolarLines();
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
            RenderingConfig.DisplayChannel = channel;
            UpdateColorDifferencePanelVisibility();

            if (HasXyzData())
            {
                try
                {
                    RefreshDisplayedImage();
                    UpdateReferencePlot();
                }
                catch (Exception ex)
                {
                    log.Error($"刷新显示通道失败: {ex.Message}", ex);
                    MessageBox.Show(ex.Message, "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ExportChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateColorDifferencePanelVisibility();
        }

        private void btnSaveConoscopeConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
                MessageBox.Show("配置已保存", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"保存 Conoscope 配置失败: {ex.Message}", ex);
                MessageBox.Show($"保存配置失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
