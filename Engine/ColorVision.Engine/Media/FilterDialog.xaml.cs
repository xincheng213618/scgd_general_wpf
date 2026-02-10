using OpenCvSharp;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public partial class FilterDialog : Window
    {
        private readonly WriteableBitmap _writeableBitmap;
        private readonly byte[] _originalPixels;

        public FilterDialog(WriteableBitmap writeableBitmap, int defaultFilter = 0)
        {
            InitializeComponent();
            _writeableBitmap = writeableBitmap;
            _originalPixels = BitmapHelper.CopyPixels(writeableBitmap);
            FilterCombo.SelectedIndex = defaultFilter;
            UpdatePanelVisibility();
        }

        private void UpdatePanelVisibility()
        {
            int idx = FilterCombo.SelectedIndex;
            // GaussianBlur: show sigma panel, hide sigmaColor
            // MedianBlur: hide both sigma panels
            // BilateralFilter: show both sigma panels
            // Blur: hide both
            SigmaPanel.Visibility = (idx == 0 || idx == 2) ? Visibility.Visible : Visibility.Collapsed;
            SigmaColorPanel.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Param_Changed(object sender, RoutedEventArgs e)
        {
            if (KernelSlider == null || FilterCombo == null) return;
            UpdatePanelVisibility();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            int kernelSize = (int)KernelSlider.Value;
            if (kernelSize % 2 == 0) kernelSize++;
            int filterIndex = FilterCombo.SelectedIndex;

            BitmapHelper.RestorePixels(_writeableBitmap, _originalPixels);

            _writeableBitmap.Lock();
            try
            {
                MatType matType = _writeableBitmap.Format.GetPixelFormat();
                using var srcMat = Mat.FromPixelData(_writeableBitmap.PixelHeight, _writeableBitmap.PixelWidth, matType, _writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride);

                switch (filterIndex)
                {
                    case 0: // GaussianBlur
                        double sigma = SigmaSlider?.Value ?? 1.5;
                        Cv2.GaussianBlur(srcMat, srcMat, new Size(kernelSize, kernelSize), sigma);
                        break;
                    case 1: // MedianBlur
                        Cv2.MedianBlur(srcMat, srcMat, kernelSize);
                        break;
                    case 2: // BilateralFilter
                        {
                            double sigmaColor = SigmaColorSlider?.Value ?? 75;
                            double sigmaSpace = SigmaSlider?.Value ?? 1.5;
                            using var temp = srcMat.Clone();
                            Cv2.BilateralFilter(temp, srcMat, kernelSize, sigmaColor, sigmaSpace);
                        }
                        break;
                    case 3: // Blur (average)
                        Cv2.Blur(srcMat, srcMat, new Size(kernelSize, kernelSize));
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
