using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CaptchaOCR
{
    public partial class CaptchaWindow : Window
    {
        private readonly CaptchaGenerator _generator;
        private readonly List<CaptchaModelInfo> _availableModels = new();
        private CaptchaRecognizer? _recognizer;
        private Bitmap? _currentImage;
        private string? _currentText;
        private bool _isInitializingModelSelection;
        private bool _isInitializingGenerationSettings;

        public CaptchaWindow()
        {
            InitializeComponent();
            _generator = new CaptchaGenerator();
            Loaded += CaptchaWindow_Loaded;
            TestCountSlider.ValueChanged += TestCountSlider_ValueChanged;
        }

        private void CaptchaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeGenerationControls();
            LoadGenerationConfig();
            LoadModels();
            GenerateNewCaptcha();
        }

        private void InitializeGenerationControls()
        {
            _isInitializingGenerationSettings = true;
            try
            {
                LengthSelector.ItemsSource = Enumerable.Range(1, 16).ToList();
                CharacterModeSelector.ItemsSource = new List<GenerationModeOption>
                {
                    new(CharacterMode.Alphanumeric, "数字字母混合"),
                    new(CharacterMode.DigitsOnly, "纯数字"),
                    new(CharacterMode.LettersOnly, "纯字母")
                };
                CharacterModeSelector.DisplayMemberPath = nameof(GenerationModeOption.Name);
            }
            finally
            {
                _isInitializingGenerationSettings = false;
            }
        }

        private void LoadGenerationConfig()
        {
            _isInitializingGenerationSettings = true;
            try
            {
                var config = ModelCatalogLoader.LoadGenerationConfig();
                _generator.Length = config.Length;
                _generator.Mode = config.Mode;
                _generator.DigitCount = config.DigitCount;

                RefreshLengthOptions();
                LengthSelector.SelectedItem = _generator.Length;
                CharacterModeSelector.SelectedItem = ((IEnumerable<GenerationModeOption>)CharacterModeSelector.ItemsSource!)
                    .First(x => x.Mode == _generator.Mode);
                RefreshDigitCountOptions();
            }
            finally
            {
                _isInitializingGenerationSettings = false;
            }
        }

        private void SaveGenerationConfig()
        {
            ModelCatalogLoader.SaveGenerationConfig(_generator.Length, _generator.Mode, _generator.DigitCount);
        }

        private void LoadModels()
        {
            _isInitializingModelSelection = true;
            try
            {
                var catalog = ModelCatalogLoader.LoadCatalog();
                _availableModels.Clear();
                _availableModels.AddRange(catalog.Models.Where(m => m.IsAvailable));

                ModelSelector.ItemsSource = null;
                ModelSelector.ItemsSource = _availableModels;

                if (_availableModels.Count == 0)
                {
                    _recognizer?.Dispose();
                    _recognizer = null;
                    ModelStatusText.Text = "未找到可用模型";
                    ModelStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                    return;
                }

                var lastSelectedId = ModelCatalogLoader.LoadLastSelected();
                var selectedModel = _availableModels.FirstOrDefault(m => m.Id == lastSelectedId) ?? _availableModels.First();
                ModelSelector.SelectedItem = selectedModel;
                SwitchModel(selectedModel, persistSelection: false);
            }
            finally
            {
                _isInitializingModelSelection = false;
            }
        }

        private void SwitchModel(CaptchaModelInfo model, bool persistSelection = true)
        {
            _recognizer?.Dispose();
            _recognizer = new CaptchaRecognizer(model);

            if (_recognizer.IsLoaded)
            {
                SyncGeneratorLength();
                RefreshLengthOptions();
                RefreshDigitCountOptions();
                ClearResult();
                ModelStatusText.Text = $"当前模型: {model.Name} ({_recognizer.MinLength}-{_recognizer.MaxLength}位)";
                ModelStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                if (persistSelection)
                    ModelCatalogLoader.SaveLastSelected(model.Id);
                SaveGenerationConfig();
            }
            else
            {
                ModelStatusText.Text = $"模型加载失败: {model.Name} - {_recognizer.ErrorMessage}";
                ModelStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void RefreshLengthOptions()
        {
            _isInitializingGenerationSettings = true;
            try
            {
                int min = _recognizer?.MinLength ?? 1;
                int max = _recognizer?.MaxLength ?? 16;
                LengthSelector.ItemsSource = Enumerable.Range(min, Math.Max(1, max - min + 1)).ToList();
                _generator.Length = Math.Clamp(_generator.Length, min, max);
                LengthSelector.SelectedItem = _generator.Length;
            }
            finally
            {
                _isInitializingGenerationSettings = false;
            }
        }

        private void RefreshDigitCountOptions()
        {
            _isInitializingGenerationSettings = true;
            try
            {
                int minDigits;
                int maxDigits;
                bool isEnabled;
                string hint;

                switch (_generator.Mode)
                {
                    case CharacterMode.DigitsOnly:
                        minDigits = maxDigits = _generator.Length;
                        _generator.DigitCount = _generator.Length;
                        isEnabled = false;
                        hint = "纯数字模式下，数字个数固定等于长度";
                        break;
                    case CharacterMode.LettersOnly:
                        minDigits = maxDigits = 0;
                        _generator.DigitCount = 0;
                        isEnabled = false;
                        hint = "纯字母模式下，数字个数固定为 0";
                        break;
                    default:
                        minDigits = 0;
                        maxDigits = _generator.Length;
                        if (_generator.DigitCount < 0 || _generator.DigitCount > _generator.Length)
                            _generator.DigitCount = Math.Min(2, _generator.Length);
                        isEnabled = true;
                        hint = "混合模式下可指定数字数量";
                        break;
                }

                DigitCountSelector.ItemsSource = Enumerable.Range(minDigits, maxDigits - minDigits + 1).ToList();
                DigitCountSelector.SelectedItem = _generator.DigitCount;
                DigitCountSelector.IsEnabled = isEnabled;
                GenerationRuleHintText.Text = hint;
            }
            finally
            {
                _isInitializingGenerationSettings = false;
            }
        }

        private void ApplyGenerationSettingsFromUi()
        {
            if (LengthSelector.SelectedItem is int length)
                _generator.Length = length;

            if (CharacterModeSelector.SelectedItem is GenerationModeOption mode)
                _generator.Mode = mode.Mode;

            RefreshDigitCountOptions();

            if (DigitCountSelector.SelectedItem is int digitCount)
                _generator.DigitCount = digitCount;

            SaveGenerationConfig();
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
                    MessageBox.Show("模型未加载，无法识别", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            System.Windows.Media.Brush brush = result.AverageConfidence > 0.9
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 118, 210))
                : result.AverageConfidence > 0.7
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));
            ResultText.Foreground = brush;

            for (int i = 0; i < 6; i++)
                SetCharResult(i, result);
        }

        private void SetCharResult(int index, RecognitionResult result)
        {
            var textBlocks = new[] { Char1Text, Char2Text, Char3Text, Char4Text, Char5Text, Char6Text };
            var bars = new[] { Char1Bar, Char2Bar, Char3Bar, Char4Bar, Char5Bar, Char6Bar };
            var confTexts = new[] { Char1Conf, Char2Conf, Char3Conf, Char4Conf, Char5Conf, Char6Conf };

            if (index < result.Text.Length && index < result.Confidences.Length)
            {
                textBlocks[index].Text = result.Text[index].ToString();
                bars[index].Value = result.Confidences[index];
                confTexts[index].Text = $"{result.Confidences[index]:P1}";
            }
            else
            {
                textBlocks[index].Text = "-";
                bars[index].Value = 0;
                confTexts[index].Text = "0%";
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
            Char5Text.Text = "-"; Char5Bar.Value = 0; Char5Conf.Text = "0%";
            Char6Text.Text = "-"; Char6Bar.Value = 0; Char6Conf.Text = "0%";
        }

        private void SyncGeneratorLength()
        {
            if (_recognizer == null)
                return;

            _generator.Length = _recognizer.MinLength == _recognizer.MaxLength
                ? _recognizer.MinLength
                : Math.Clamp(_generator.Length, _recognizer.MinLength, _recognizer.MaxLength);
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

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            GenerateNewCaptcha();
            ClearResult();
        }

        private void BtnRecognize_Click(object sender, RoutedEventArgs e)
        {
            RecognizeCurrentImage();
        }

        private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingModelSelection)
                return;

            if (ModelSelector.SelectedItem is CaptchaModelInfo model)
                SwitchModel(model);
        }

        private void GenerationSetting_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingGenerationSettings)
                return;

            ApplyGenerationSettingsFromUi();
            GenerateNewCaptcha();
            ClearResult();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
                Title = "选择验证码图片"
            };

            if (dialog.ShowDialog() == true)
                FilePathText.Text = dialog.FileName;
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
                    DisplayResult(result);
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
            var generator = new CaptchaGenerator
            {
                Length = _generator.Length,
                Mode = _generator.Mode,
                DigitCount = _generator.DigitCount
            };

            for (int i = 0; i < count; i++)
            {
                var (image, trueText) = generator.Generate();
                var result = _recognizer.Recognize(image);
                image.Dispose();

                if (result != null)
                {
                    totalConfidence += result.AverageConfidence;
                    if (string.Equals(result.Text, trueText, StringComparison.OrdinalIgnoreCase))
                        correct++;
                }
            }

            double accuracy = count > 0 ? (double)correct / count * 100 : 0;
            double avgConf = count > 0 ? totalConfidence / count : 0;

            string modelName = _recognizer.ModelInfo.Name;
            BatchResultText.Text = $"模型: {modelName} | 测试数量: {count} | 正确: {correct} | 准确率: {accuracy:F2}% | 平均置信度: {avgConf:P2}";
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

    public sealed class GenerationModeOption
    {
        public GenerationModeOption(CharacterMode mode, string name)
        {
            Mode = mode;
            Name = name;
        }

        public CharacterMode Mode { get; }
        public string Name { get; }
    }
}
