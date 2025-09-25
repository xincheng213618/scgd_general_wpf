using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ͼ��任�����࣬������ת����ת��ͼ��任����
    /// </summary>
    public class ImageTransformOperations
    {
        private readonly DrawCanvas _image;

        public ImageTransformOperations(DrawCanvas image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        /// <summary>
        /// ˮƽ��ת
        /// </summary>
        public void FlipHorizontal()
        {
            if (_image.RenderTransform is TransformGroup transformGroup)
            {
                var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (scaleTransform != null)
                {
                    scaleTransform.ScaleX *= -1;
                }
                else
                {
                    scaleTransform = new ScaleTransform { ScaleX = -1 };
                    transformGroup.Children.Add(scaleTransform);
                }
            }
            else
            {
                transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform { ScaleX = -1 });
                _image.RenderTransform = transformGroup;
                _image.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        /// <summary>
        /// ��ֱ��ת
        /// </summary>
        public void FlipVertical()
        {
            if (_image.RenderTransform is TransformGroup transformGroup)
            {
                var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (scaleTransform != null)
                {
                    scaleTransform.ScaleY *= -1;
                }
                else
                {
                    scaleTransform = new ScaleTransform { ScaleY = -1 };
                    transformGroup.Children.Add(scaleTransform);
                }
            }
            else
            {
                transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform { ScaleY = -1 });
                _image.RenderTransform = transformGroup;
                _image.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        /// <summary>
        /// ������ת
        /// </summary>
        public void RotateRight()
        {
            if (_image.RenderTransform is RotateTransform rotateTransform)
            {
                rotateTransform.Angle += 90;
            }
            else
            {
                RotateTransform rotateTransform1 = new() { Angle = 90 };
                _image.RenderTransform = rotateTransform1;
                _image.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        /// <summary>
        /// ������ת
        /// </summary>
        public void RotateLeft()
        {
            if (_image.RenderTransform is RotateTransform rotateTransform)
            {
                rotateTransform.Angle -= 90;
            }
            else
            {
                RotateTransform rotateTransform1 = new() { Angle = -90 };
                _image.RenderTransform = rotateTransform1;
                _image.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }
    }
}
