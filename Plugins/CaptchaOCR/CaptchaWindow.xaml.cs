using Microsoft.Win32;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CaptchaOCR
{
    public partial class CaptchaWindow : Window
    {
        private readonly CaptchaGenerator _generator;
        private CaptchaRecognizer? _recognizer;
        private Bitmap? _currentImage;
        private string? _currentText;

        public CaptchaWindow()
        {
            InitializeComponent();
            _generator = new CaptchaGenerator();
            Loaded += CaptchaWindow_Loaded;
            TestCountSlider.ValueChanged += TestCountSlider_ValueChanged;
        }

        private void CaptchaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载模型
            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "captcha_model.onnx");
            if (!File.Exists(modelPath))
            {
                // 尝试其他路径
                string[] possiblePaths =
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "captcha_model.onnx"),
                    Path.Combine(Directory.GetCurrentDirectory(), "captcha_model.onnx"),
                    @"..\..\..\..\captcha_ocr\models\captcha_model.onnx",
                    @"C:\Users\17917\Desktop\captcha_ocr\captcha_ocr\models\captcha_model.onnx"
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        modelPath = path;
                        break;
                    }
                }
            }

            if (File.Exists(modelPath))
            {
                _recognizer = new CaptchaRecognizer(modelPath);
                if (_recognizer.IsLoaded)
                {
                    ModelStatusText.Text = "模型已加载";
                    ModelStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                }
                else
                {
                    ModelStatusText.Text = $"模型加载失败: {_recognizer.ErrorMessage}";
                    ModelStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                }
            }
            else
            {
                ModelStatusText.Text = "未找到模型文件 captcha_model.onnx";
                ModelStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }

            // 生成初始验证码
            GenerateNewCaptcha();
        }

        private void GenerateNewCaptcha()
        {
            _currentImage?.Dispose();
            var (image, text) = _generator.Generate();
            _currentImage = image;
            _currentText = text;

            GeneratedImage.Source = BitmapToBitmapSource(image);
        }

        private void RecognizeCurrentImage()
        {
            if (_currentImage == null || _recognizer == null || !_recognizer.IsLoaded)
            {
                if (_recognizer == null || !_recognizer.IsLoaded)
                {
                    MessageBox.Show("模型未加载，无法识别", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }

            var result = _recognizer.Recognize(_currentImage);
            if (result != null)
            {
                DisplayResult(result);
            }
            else
            {
                MessageBox.Show($"识别失败: {_recognizer.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayResult(RecognitionResult result)
        {
            ResultText.Text = result.Text;
            AvgConfidenceBar.Value = result.AverageConfidence;
            AvgConfidenceText.Text = $"{result.AverageConfidence:P1}";

            // 设置颜色 - 根据准确率
            var brush = result.AverageConfidence > 0.9
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 118, 210))
                : result.AverageConfidence > 0.7
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));
            ResultText.Foreground = brush;

            // 各位置结果
            if (result.Confidences.Length >= 4)
            {
                Char1Text.Text = result.Text.Length > 0 ? result.Text[0].ToString() : "-";
                Char1Bar.Value = result.Confidences[0];
                Char1Conf.Text = $"{result.Confidences[0]:P1}";

                Char2Text.Text = result.Text.Length > 1 ? result.Text[1].ToString() : "-";
                Char2Bar.Value = result.Confidences[1];
                Char2Conf.Text = $"{result.Confidences[1]:P1}";

                Char3Text.Text = result.Text.Length > 2 ? result.Text[2].ToString() : "-";
                Char3Bar.Value = result.Confidences[2];
                Char3Conf.Text = $"{result.Confidences[2]:P1}";

                Char4Text.Text = result.Text.Length > 3 ? result.Text[3].ToString() : "-";
                Char4Bar.Value = result.Confidences[3];
                Char4Conf.Text = $"{result.Confidences[3]:P1}";
            }
        }

        private void ClearResult()
        {
            ResultText.Text = "--";
            ResultText.Foreground = System.Windows.Media.Brushes.Gray;
            AvgConfidenceBar.Value = 0;
            AvgConfidenceText.Text = "0%";

            Char1Text.Text = "-"; Char1Bar.Value = 0; Char1Conf.Text = "0%";
            Char2Text.Text = "-"; Char2Bar.Value = 0; Char2Conf.Text = "0%";
            Char3Text.Text = "-"; Char3Bar.Value = 0; Char3Conf.Text = "0%";
            Char4Text.Text = "-"; Char4Bar.Value = 0; Char4Conf.Text = "0%";
        }

        private static BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }

        // ========== 事件处理 ==========

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            GenerateNewCaptcha();
            ClearResult();
        }

        private void BtnRecognize_Click(object sender, RoutedEventArgs e)
        {
            RecognizeCurrentImage();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
                Title = "选择验证码图片"
            };

            if (dialog.ShowDialog() == true)
            {
                FilePathText.Text = dialog.FileName;
            }
        }

        private void BtnRecognizeFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FilePathText.Text) || !File.Exists(FilePathText.Text))
            {
                MessageBox.Show("请先选择有效的图片文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_recognizer == null || !_recognizer.IsLoaded)
            {
                MessageBox.Show("模型未加载", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using var bitmap = new Bitmap(FilePathText.Text);
                _currentImage?.Dispose();
                _currentImage = new Bitmap(bitmap);
                GeneratedImage.Source = BitmapToBitmapSource(bitmap);

                var result = _recognizer.Recognize(bitmap);
                if (result != null)
                {
                    DisplayResult(result);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"识别失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBatchTest_Click(object sender, RoutedEventArgs e)
        {
            if (_recognizer == null || !_recognizer.IsLoaded)
            {
                MessageBox.Show("模型未加载", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int count = (int)TestCountSlider.Value;
            int correct = 0;
            double totalConfidence = 0;
            var generator = new CaptchaGenerator();

            for (int i = 0; i < count; i++)
            {
                var (image, trueText) = generator.Generate();
                var result = _recognizer.Recognize(image);
                image.Dispose();

                if (result != null)
                {
                    totalConfidence += result.AverageConfidence;
                    if (result.Text == trueText)
                    {
                        correct++;
                    }
                }
            }

            double accuracy = (double)correct / count * 100;
            double avgConf = totalConfidence / count;

            BatchResultText.Text = $"测试数量: {count} | 正确: {correct} | 准确率: {accuracy:F2}% | 平均置信度: {avgConf:P2}";
            BatchResultText.Foreground = accuracy > 95
                ? System.Windows.Media.Brushes.Green
                : accuracy > 80
                    ? System.Windows.Media.Brushes.Orange
                    : System.Windows.Media.Brushes.Red;
        }

        private void TestCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TestCountText.Text = ((int)e.NewValue).ToString();
        }

        protected override void OnClosed(EventArgs e)
        {
            _currentImage?.Dispose();
            _recognizer?.Dispose();
            base.OnClosed(e);
        }
    }
}
