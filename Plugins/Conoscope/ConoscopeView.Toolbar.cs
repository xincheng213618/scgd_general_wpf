using ColorVision.ImageEditor;
using Conoscope.Core;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void UpdatePanModeState()
        {
            bool isFocusCircleInteractionEnabled = ImageView.IsFocusCircleEditMode || ImageView.IsFocusCircleSelectionEnabled;
            ImageView.Zoombox1.ActivateOn = isFocusCircleInteractionEnabled ? ModifierKeys.Control : ModifierKeys.None;
            if (!ImageView.IsFocusCircleEditMode)
            {
                ImageView.Zoombox1.Cursor = Cursors.Arrow;
                ImageView.ImageShow.Cursor = Cursors.Arrow;
            }
        }

        private void btnCircleFit_Click(object sender, RoutedEventArgs e)
        {
            ApplyCircleFitZoomMode();
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
                    break;
                case ConoscopeImageZoomMode.Fit:
                default:
                    ApplyImageZoomMode(ConoscopeImageZoomMode.Fit, () => ImageView.UpdateZoomAndScale());
                    break;
            }
        }

        private void ApplyCircleFitZoomMode()
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
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.Msg3DViewOpenFailed, ex.Message), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private WriteableBitmap Create3DHeightBitmapForCurrentView()
        {
            OpenCvSharp.Mat fallback = YMat!;
            return ConoscopePseudoColorRenderer.CreateHeightMapBitmap(
                XMat!,
                YMat!,
                ZMat!,
                GetSelectedDisplayChannel(),
                () => CreateColorDifferenceMat() ?? fallback,
                () => CreateContrastMat() ?? fallback,
                currentImageCenter,
                currentImageRadius);
        }
    }
}
