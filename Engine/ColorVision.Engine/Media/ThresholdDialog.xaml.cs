using OpenCvSharp;
using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public partial class ThresholdDialog : System.Windows.Window
    {
        private readonly WriteableBitmap _writeableBitmap;
        private readonly byte[] _originalPixels;

        public ThresholdDialog(WriteableBitmap writeableBitmap)
        {
            InitializeComponent();
            _writeableBitmap = writeableBitmap;
            _originalPixels = BitmapHelper.CopyPixels(writeableBitmap);
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThresholdSlider == null || MaxValSlider == null) return;
            ApplyThreshold();
        }

        private void ApplyThreshold()
        {
            double thresh = ThresholdSlider.Value;
            double maxVal = MaxValSlider.Value;

            BitmapHelper.RestorePixels(_writeableBitmap, _originalPixels);

            _writeableBitmap.Lock();
            try
            {
                MatType matType = _writeableBitmap.Format.GetPixelFormat();
                using var srcMat = Mat.FromPixelData(_writeableBitmap.PixelHeight, _writeableBitmap.PixelWidth, matType, _writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride);

                if (srcMat.Channels() > 1)
                {
                    using var gray = new Mat();
                    Cv2.CvtColor(srcMat, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.Threshold(gray, gray, thresh, maxVal, ThresholdTypes.Binary);
                    Cv2.CvtColor(gray, srcMat, ColorConversionCodes.GRAY2BGR);
                }
                else
                {
                    Cv2.Threshold(srcMat, srcMat, thresh, maxVal, ThresholdTypes.Binary);
                }

                _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, _writeableBitmap.PixelWidth, _writeableBitmap.PixelHeight));
            }
            finally
            {
                _writeableBitmap.Unlock();
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            BitmapHelper.RestorePixels(_writeableBitmap, _originalPixels);
            DialogResult = false;
            Close();
        }
    }
}
