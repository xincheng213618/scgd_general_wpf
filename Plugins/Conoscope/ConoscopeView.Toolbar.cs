using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using Conoscope.Core;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void UpdateToolbarZoomRatio()
        {
            if (txtToolbarZoomRatio == null)
            {
                return;
            }

            double zoomRatio = ImageView.Zoombox1.ContentMatrix.M11;
            txtToolbarZoomRatio.Text = double.IsFinite(zoomRatio) ? zoomRatio.ToString("F2", CultureInfo.InvariantCulture) : "1.00";
        }

        private void UpdatePanModeState()
        {
            bool isPanModeEnabled = tglPanMode?.IsChecked == true;
            ImageView.Zoombox1.ActivateOn = isPanModeEnabled ? ModifierKeys.None : ModifierKeys.Control;
            ImageView.Zoombox1.Cursor = isPanModeEnabled ? Cursors.Hand : Cursors.Arrow;
        }

        private void tglPanMode_Checked(object sender, RoutedEventArgs e)
        {
            UpdatePanModeState();
        }

        private void tglPanMode_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdatePanModeState();
        }

        private void btnOpenCieWindow_Click(object sender, RoutedEventArgs e)
        {
            OpenCieWindow();
        }

        private void ToolbarOpenCie_Click(object sender, RoutedEventArgs e)
        {
            OpenCieWindow();
        }

        internal void OpenCieForCurrentView()
        {
            OpenCieWindow();
        }

        private void OpenCieWindow()
        {
            if (!HasXyzData() || currentBitmapSource == null || coordinateAxisController == null)
            {
                MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EnsureCieWindow();
            SyncCieWindowFromCurrentPointer();
        }

        private void EnsureCieWindow()
        {
            if (cieWindow == null)
            {
                cieWindow = new WindowCIE();
                Window? owner = Window.GetWindow(this);
                if (owner != null)
                {
                    cieWindow.Owner = owner;
                }

                cieWindow.Closed += (_, _) => cieWindow = null;
            }

            cieWindow.Show();
            cieWindow.Activate();
        }

        private void SyncCieWindowFromCurrentPointer()
        {
            if (cieWindow == null || coordinateAxisController == null)
            {
                return;
            }

            Point point = Mouse.GetPosition(ImageView.ImageShow);
            if (!coordinateAxisController.Axis.ContainsInteractivePoint(point))
            {
                return;
            }

            UpdateCieWindowSelection(point);
        }

        private void ToolbarZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.Zoom(1.25);
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.Zoom(0.8);
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomNone_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.ZoomNone();
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomUniform_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.ZoomUniform();
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomUniformToFill_Click(object sender, RoutedEventArgs e)
        {
            ImageView.Zoombox1.ZoomUniformToFill();
            UpdateToolbarZoomRatio();
        }

        private void txtToolbarZoomRatio_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyToolbarZoomRatio();
                e.Handled = true;
            }
        }

        private void txtToolbarZoomRatio_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyToolbarZoomRatio();
        }

        private void ApplyToolbarZoomRatio()
        {
            if (txtToolbarZoomRatio == null)
            {
                return;
            }

            if (!ConoscopeNumericHelper.TryParseDouble(txtToolbarZoomRatio.Text, out double zoomRatio)
                || !double.IsFinite(zoomRatio)
                || zoomRatio <= 0)
            {
                UpdateToolbarZoomRatio();
                return;
            }

            double currentZoom = ImageView.Zoombox1.ContentMatrix.M11;
            if (!double.IsFinite(currentZoom) || currentZoom <= 0)
            {
                currentZoom = 1;
            }

            ImageView.Zoombox1.Zoom(zoomRatio / currentZoom);
            UpdateToolbarZoomRatio();
        }

        private void ToolbarFullScreen_Click(object sender, RoutedEventArgs e)
        {
            imageFullScreenMode ??= new ImageFullScreenMode(ImageViewHost);
            imageFullScreenMode.ToggleFullScreen();
        }

        private void ToolbarOpen3D_Click(object sender, RoutedEventArgs e)
        {
            Open3DForCurrentView();
        }

        internal void Open3DForCurrentView()
        {
            if (!HasXyzData() || currentBitmapSource == null)
            {
                MessageBox.Show("当前图像尚未准备好 3D 视图", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                WriteableBitmap heightBitmap = Create3DHeightBitmapForCurrentView();
                Window3D window3D = new(heightBitmap, Conoscope3DInitialHeightScale)
                {
                    Owner = Window.GetWindow(this)
                };
                window3D.Show();
            }
            catch (Exception ex)
            {
                log.Error("打开 Conoscope 3D 视图失败", ex);
                MessageBox.Show($"打开 3D 视图失败: {ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private WriteableBitmap Create3DHeightBitmapForCurrentView()
        {
            return ConoscopePseudoColorRenderer.CreateHeightMapBitmap(
                XMat!,
                YMat!,
                ZMat!,
                GetSelectedDisplayChannel(),
                CreateColorDifferenceMat,
                currentImageCenter,
                currentImageRadius);
        }
    }
}