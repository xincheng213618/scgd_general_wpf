using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using Conoscope.Core;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
            bool isFocusCircleInteractionEnabled = ImageView.IsFocusCircleEditMode || ImageView.IsFocusCircleSelectionEnabled;
            bool isPanModeEnabled = tglPanMode?.IsChecked == true && !isFocusCircleInteractionEnabled;
            ImageView.Zoombox1.ActivateOn = isPanModeEnabled ? ModifierKeys.None : ModifierKeys.Control;
            if (!ImageView.IsFocusCircleEditMode)
            {
                ImageView.Zoombox1.Cursor = isPanModeEnabled ? Cursors.Hand : Cursors.Arrow;
                ImageView.ImageShow.Cursor = isPanModeEnabled ? Cursors.Hand : Cursors.Arrow;
            }
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
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
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
            imageZoomMode = ConoscopeImageZoomMode.Custom;
            ImageView.Zoombox1.Zoom(1.25);
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomOut_Click(object sender, RoutedEventArgs e)
        {
            imageZoomMode = ConoscopeImageZoomMode.Custom;
            ImageView.Zoombox1.Zoom(0.8);
            UpdateToolbarZoomRatio();
        }

        private void ToolbarZoomNone_Click(object sender, RoutedEventArgs e)
        {
            ApplyImageZoomMode(ConoscopeImageZoomMode.ActualSize, () => ImageView.Zoombox1.ZoomNone());
        }

        private void ToolbarZoomUniform_Click(object sender, RoutedEventArgs e)
        {
            ApplyImageZoomMode(ConoscopeImageZoomMode.Fit, () => ImageView.Zoombox1.ZoomUniform());
        }

        private void ToolbarZoomUniformToFill_Click(object sender, RoutedEventArgs e)
        {
            ApplyImageZoomMode(ConoscopeImageZoomMode.Fill, () => ImageView.Zoombox1.ZoomUniformToFill());
        }

        private void ToolbarZoomCircleFit_Click(object sender, RoutedEventArgs e)
        {
            if (!HasXyzData())
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ApplyImageZoomMode(ConoscopeImageZoomMode.CircleFit, () =>
            {
                if (!TryApplyCircleFitZoom())
                {
                    ImageView.Zoombox1.ZoomUniform();
                }
            });
        }

        private void ApplyZoomAfterDisplayRefresh()
        {
            if (applyCircleFitOnNextRefresh)
            {
                applyCircleFitOnNextRefresh = false;
                imageZoomMode = ConoscopeImageZoomMode.CircleFit;
            }

            switch (imageZoomMode)
            {
                case ConoscopeImageZoomMode.ActualSize:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.ActualSize, () => ImageView.Zoombox1.ZoomNone());
                    break;
                case ConoscopeImageZoomMode.Fill:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.Fill, () => ImageView.Zoombox1.ZoomUniformToFill());
                    break;
                case ConoscopeImageZoomMode.CircleFit:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.CircleFit, () =>
                    {
                        if (!TryApplyCircleFitZoom())
                        {
                            ImageView.Zoombox1.ZoomUniform();
                        }
                    });
                    break;
                case ConoscopeImageZoomMode.Custom:
                    UpdateToolbarZoomRatio();
                    break;
                case ConoscopeImageZoomMode.Fit:
                default:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.Fit, () => ImageView.UpdateZoomAndScale());
                    break;
            }
        }

        private void ApplyImageZoomMode(ConoscopeImageZoomMode zoomMode, Action zoomAction)
        {
            imageZoomMode = zoomMode;
            isApplyingImageZoomMode = true;
            try
            {
                zoomAction();
            }
            finally
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => isApplyingImageZoomMode = false));
            }

            UpdateToolbarZoomRatio();
        }

        private bool TryApplyCircleFitZoom()
        {
            if (!TryGetCurrentCircleBounds(out Rect circleBounds))
            {
                return false;
            }

            ImageView.ZoomToImageRect(circleBounds);
            return true;
        }

        private bool TryGetCurrentCircleBounds(out Rect circleBounds)
        {
            circleBounds = Rect.Empty;

            int imageWidth = currentBitmapSource?.PixelWidth ?? XMat?.Width ?? 0;
            int imageHeight = currentBitmapSource?.PixelHeight ?? XMat?.Height ?? 0;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return false;
            }

            Point center = currentImageCenter;
            double radius = currentImageRadius;
            if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || radius <= 0)
            {
                center = new Point(imageWidth / 2.0, imageHeight / 2.0);
                double pixelsPerDegree = CurrentModelProfile.GetConoscopeCoefficient(imageWidth, imageHeight);
                radius = MaxAngle * pixelsPerDegree;
            }

            if (!double.IsFinite(radius) || radius <= 0)
            {
                return false;
            }

            double left = Math.Max(0, center.X - radius);
            double top = Math.Max(0, center.Y - radius);
            double right = Math.Min(imageWidth, center.X + radius);
            double bottom = Math.Min(imageHeight, center.Y + radius);
            double width = right - left;
            double height = bottom - top;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            circleBounds = new Rect(left, top, width, height);
            return true;
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

            imageZoomMode = ConoscopeImageZoomMode.Custom;
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
                MessageBox.Show(Properties.Resources.Msg3DViewNotReady, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(string.Format(Properties.Resources.Msg3DViewOpenFailed, ex.Message), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
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
