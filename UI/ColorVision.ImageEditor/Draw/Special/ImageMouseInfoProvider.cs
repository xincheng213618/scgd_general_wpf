using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Utils;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Special
{
    public interface IImageMouseInfoProvider
    {
        event MouseMoveColorHandler? MouseMoveColorHandler;
    }

    public sealed class ImageMouseInfoProvider : IImageMouseInfoProvider, IDisposable
    {
        private readonly EditorContext _editorContext;
        private MouseMoveColorHandler? _mouseMoveColorHandler;

        private DrawCanvas Image => _editorContext.DrawCanvas;

        public ImageMouseInfoProvider(EditorContext editorContext)
        {
            _editorContext = editorContext;
        }

        public event MouseMoveColorHandler? MouseMoveColorHandler
        {
            add
            {
                bool shouldAttach = _mouseMoveColorHandler == null;
                _mouseMoveColorHandler += value;
                if (shouldAttach)
                {
                    Image.MouseMove += HandleMouseMove;
                }
            }
            remove
            {
                _mouseMoveColorHandler -= value;
                if (_mouseMoveColorHandler == null)
                {
                    Image.MouseMove -= HandleMouseMove;
                }
            }
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            if (_mouseMoveColorHandler == null || Image.Source is not BitmapSource bitmap)
            {
                return;
            }

            if (Image.ActualWidth <= 0 || Image.ActualHeight <= 0)
            {
                return;
            }

            Point point = e.GetPosition(Image);
            Point actPoint = new(point.X, point.Y);
            point.X = point.X / Image.ActualWidth * bitmap.PixelWidth;
            point.Y = point.Y / Image.ActualHeight * bitmap.PixelHeight;

            int pixelX = point.X.ToInt32();
            int pixelY = point.Y.ToInt32();
            if (pixelX < 0 || pixelX >= bitmap.PixelWidth || pixelY < 0 || pixelY >= bitmap.PixelHeight)
            {
                return;
            }

            (int red, int green, int blue) = ImageEditorUtils.GetPixelColor(bitmap, pixelX, pixelY);
            ImageInfo imageInfo = new()
            {
                ActPoint = actPoint,
                BitmapPoint = new Point(pixelX, pixelY),
                X = pixelX,
                Y = pixelY,
                R = red,
                G = green,
                B = blue,
            };

            _mouseMoveColorHandler?.Invoke(this, imageInfo);
        }

        public void Dispose()
        {
            Image.MouseMove -= HandleMouseMove;
            _mouseMoveColorHandler = null;
            GC.SuppressFinalize(this);
        }
    }
}