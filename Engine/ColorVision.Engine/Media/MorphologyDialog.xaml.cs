using OpenCvSharp;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public partial class MorphologyDialog : Window
    {
        private readonly WriteableBitmap _writeableBitmap;
        private readonly byte[] _originalPixels;

        public MorphologyDialog(WriteableBitmap writeableBitmap, int defaultOperation = 0)
        {
            InitializeComponent();
            _writeableBitmap = writeableBitmap;
            _originalPixels = BitmapHelper.CopyPixels(writeableBitmap);
            OperationCombo.SelectedIndex = defaultOperation;
        }

        private void Param_Changed(object sender, RoutedEventArgs e)
        {
            if (KernelSlider == null || IterSlider == null || OperationCombo == null) return;
            ApplyMorphology();
        }

        private void ApplyMorphology()
        {
            int kernelSize = (int)KernelSlider.Value;
            if (kernelSize % 2 == 0) kernelSize++;
            int iterations = (int)IterSlider.Value;
            int opIndex = OperationCombo.SelectedIndex;

            BitmapHelper.RestorePixels(_writeableBitmap, _originalPixels);

            _writeableBitmap.Lock();
            try
            {
                MatType matType = _writeableBitmap.Format.GetPixelFormat();
                using var srcMat = Mat.FromPixelData(_writeableBitmap.PixelHeight, _writeableBitmap.PixelWidth, matType, _writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride);
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize));

                switch (opIndex)
                {
                    case 0: // Erode
                        Cv2.Erode(srcMat, srcMat, kernel, iterations: iterations);
                        break;
                    case 1: // Dilate
                        Cv2.Dilate(srcMat, srcMat, kernel, iterations: iterations);
                        break;
                    case 2: // Open
                        Cv2.MorphologyEx(srcMat, srcMat, MorphTypes.Open, kernel, iterations: iterations);
                        break;
                    case 3: // Close
                        Cv2.MorphologyEx(srcMat, srcMat, MorphTypes.Close, kernel, iterations: iterations);
                        break;
                    case 4: // Gradient
                        Cv2.MorphologyEx(srcMat, srcMat, MorphTypes.Gradient, kernel, iterations: iterations);
                        break;
                    case 5: // TopHat
                        Cv2.MorphologyEx(srcMat, srcMat, MorphTypes.TopHat, kernel, iterations: iterations);
                        break;
                    case 6: // BlackHat
                        Cv2.MorphologyEx(srcMat, srcMat, MorphTypes.BlackHat, kernel, iterations: iterations);
                        break;
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
