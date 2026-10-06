using ColorVision.Algorithms;
using ColorVision.UI;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Themes;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.BatchProcessing
{
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The token source is owned only while a batch run is active and is disposed in Execute_Click.")]
    public partial class BatchImageProcessingWindow : System.Windows.Window
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BatchImageProcessingWindow));
        private readonly BatchImageProcessor _processor;
        private readonly IReadOnlyList<BatchImageAlgorithmDefinition> _algorithms;
        private CancellationTokenSource? _cancellationTokenSource;

        public BatchImageProcessingWindow()
            : this(ImageAlgorithmPlatform.Runtime)
        {
        }

        public BatchImageProcessingWindow(AlgorithmRuntime runtime)
            : this(BatchImageAlgorithms.CreateAll(runtime))
        {
        }

        public BatchImageProcessingWindow(IReadOnlyList<BatchImageAlgorithmDefinition> algorithms)
            : this(algorithms, LoadImageLoaders())
        {
        }

        internal BatchImageProcessingWindow(
            IReadOnlyList<BatchImageAlgorithmDefinition> algorithms,
            IReadOnlyList<IBatchImageLoader> loaders)
        {
            ArgumentNullException.ThrowIfNull(algorithms);
            ArgumentNullException.ThrowIfNull(loaders);
            InitializeComponent();
            this.ApplyCaption();
            DataContext = this;

            _processor = new BatchImageProcessor(loaders);
            _algorithms = algorithms.ToArray();
            AlgorithmComboBox.ItemsSource = _algorithms;
            AlgorithmComboBox.SelectedIndex = 0;

            OutputFormatComboBox.ItemsSource = new[]
            {
                new BatchOutputFormatItem(BatchOutputFormat.SameAsSource, Properties.Resources.BatchSameAsSource),
                new BatchOutputFormatItem(BatchOutputFormat.Png, "PNG"),
                new BatchOutputFormatItem(BatchOutputFormat.Jpeg, "JPEG"),
                new BatchOutputFormatItem(BatchOutputFormat.Bmp, "BMP"),
                new BatchOutputFormatItem(BatchOutputFormat.Tiff, "TIFF"),
                new BatchOutputFormatItem(BatchOutputFormat.WebP, "WebP"),
            };
            OutputFormatComboBox.SelectedIndex = 0;
            UpdateFileCount();
        }

        public ObservableCollection<BatchImageItem> Files { get; } = new();

        internal IReadOnlyList<BatchImageAlgorithmDefinition> Algorithms => _algorithms;

        private static IBatchImageLoader[] LoadImageLoaders()
        {
            List<IBatchImageLoader> loaders = new() { new StandardBatchImageLoader() };
            try
            {
                loaders.AddRange(AssemblyHandler.Instance.LoadImplementations<IBatchImageLoader>());
            }
            catch (Exception ex)
            {
                Log.Warn("Discovering batch image loaders failed.", ex);
            }

            return loaders.GroupBy(loader => loader.GetType()).Select(group => group.First()).ToArray();
        }

        private void AlgorithmComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AlgorithmComboBox.SelectedItem is not BatchImageAlgorithmDefinition algorithm)
            {
                return;
            }

            SuffixTextBox.Text = algorithm.Suffix;
            AlgorithmOptionsContent.Content = algorithm.IsFormatOnly || algorithm.Options is NoAlgorithmParameters
                ? new TextBlock { Text = Properties.Resources.BatchNoExtraParameters, Opacity = 0.7 }
                : PropertyEditorHelper.GenPropertyEditorControl(algorithm.Options, showCategoryHeader: false);
        }

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.OpenFileDialog dialog = new()
            {
                Multiselect = true,
                RestoreDirectory = true,
                Filter = BuildFileDialogFilter(),
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            AddFiles(dialog.FileNames, null);
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.FolderBrowserDialog dialog = new()
            {
                Description = Properties.Resources.BatchSelectInputFolder,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            try
            {
                SearchOption searchOption = RecursiveCheckBox.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                IEnumerable<string> files = Directory.EnumerateFiles(dialog.SelectedPath, "*", searchOption)
                    .Where(IsSupportedFile);
                AddFiles(files, dialog.SelectedPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                MessageBox.Show(this, ex.Message, Properties.Resources.BatchAddFolderFailed, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddFiles(IEnumerable<string> filePaths, string? sourceRoot)
        {
            HashSet<string> existing = Files.Select(item => item.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string filePath in filePaths.Where(IsSupportedFile).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string fullPath = Path.GetFullPath(filePath);
                if (existing.Add(fullPath))
                {
                    Files.Add(new BatchImageItem(fullPath, sourceRoot));
                }
            }

            UpdateFileCount();
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (BatchImageItem item in FilesDataGrid.SelectedItems.Cast<BatchImageItem>().ToArray())
            {
                Files.Remove(item);
            }

            UpdateFileCount();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Files.Clear();
            UpdateFileCount();
        }

        private void BrowseOutputDirectory_Click(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.FolderBrowserDialog dialog = new()
            {
                Description = Properties.Resources.BatchSelectOutputFolder,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(OutputDirectoryTextBox.Text) ? OutputDirectoryTextBox.Text : string.Empty,
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutputDirectoryTextBox.Text = dialog.SelectedPath;
            }
        }

        private async void Execute_Click(object sender, RoutedEventArgs e)
        {
            if (Files.Count == 0)
            {
                MessageBox.Show(this, Properties.Resources.BatchAddImagesFirst, Properties.Resources.BatchRunTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (AlgorithmComboBox.SelectedItem is not BatchImageAlgorithmDefinition algorithm
                || OutputFormatComboBox.SelectedItem is not BatchOutputFormatItem outputFormat)
            {
                return;
            }

            string suffix = SuffixTextBox.Text ?? string.Empty;
            if (suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, Properties.Resources.BatchInvalidSuffix, Properties.Resources.BatchRunTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? outputDirectory;
            try
            {
                outputDirectory = string.IsNullOrWhiteSpace(OutputDirectoryTextBox.Text)
                    ? null
                    : Path.GetFullPath(OutputDirectoryTextBox.Text.Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(Properties.Resources.BatchInvalidOutputFolder, ex.Message), Properties.Resources.BatchRunTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool preserveFolderStructure = PreserveFolderStructureCheckBox.IsChecked == true;
            bool avoidOverwrite = AvoidOverwriteCheckBox.IsChecked == true;
            BatchImageItem[] items = Files.ToArray();

            SetProcessingState(true, items.Length);
            _cancellationTokenSource = new CancellationTokenSource();
            BatchImageRunResult summary;
            try
            {
                summary = await Task.Run(() => _processor.Process(
                    new BatchImageProcessingRequest
                    {
                        Items = items,
                        Algorithm = algorithm,
                        OutputFormat = outputFormat.Value,
                        OutputDirectory = outputDirectory,
                        Suffix = suffix,
                        PreserveFolderStructure = preserveFolderStructure,
                        AvoidOverwrite = avoidOverwrite,
                    },
                    UpdateItem,
                    _cancellationTokenSource.Token));
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                SetProcessingState(false, items.Length);
            }

            foreach (var failure in summary.Files.Where(file => !file.Success && !file.Cancelled))
            {
                Log.Error($"Batch processing failed for '{failure.SourcePath}': {failure.ErrorMessage}");
            }

            string message = summary.Cancelled
                ? string.Format(Properties.Resources.BatchCanceledSummary, summary.Succeeded, summary.Failed)
                : string.Format(Properties.Resources.BatchCompletedSummary, summary.Succeeded, summary.Failed);
            MessageBox.Show(this, message, Properties.Resources.BatchRunTitle, MessageBoxButton.OK,
                summary.Failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void UpdateItem(BatchImageProgress progress)
        {
            Dispatcher.Invoke(() =>
            {
                progress.Item.Status = progress.Status;
                if (progress.OutputPath != null)
                {
                    progress.Item.OutputPath = progress.OutputPath;
                }
                ProgressBar.Value = progress.Completed;
                ProgressTextBlock.Text = $"{progress.Completed} / {progress.Total}";
            });
        }

        private bool IsSupportedFile(string filePath) => _processor.IsSupported(filePath);

        private string BuildFileDialogFilter()
        {
            string[] extensions = _processor.SupportedExtensions
                .Select(extension => $"*{extension}")
                .ToArray();
            string pattern = string.Join(';', extensions);
            return string.Format(Properties.Resources.BatchFileFilter, pattern);
        }

        private void SetProcessingState(bool isProcessing, int total)
        {
            SettingsPanel.IsEnabled = !isProcessing;
            AddFilesButton.IsEnabled = !isProcessing;
            AddFolderButton.IsEnabled = !isProcessing;
            RemoveButton.IsEnabled = !isProcessing;
            ClearButton.IsEnabled = !isProcessing;
            RecursiveCheckBox.IsEnabled = !isProcessing;
            ExecuteButton.IsEnabled = !isProcessing;
            CancelButton.IsEnabled = isProcessing;
            ProgressBar.Maximum = total;
            if (isProcessing)
            {
                ProgressBar.Value = 0;
                ProgressTextBlock.Text = $"0 / {total}";
            }
        }

        private void UpdateFileCount()
        {
            FileCountTextBlock.Text = string.Format(Properties.Resources.BatchFileCount, Files.Count);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            CancelButton.IsEnabled = false;
            ProgressTextBlock.Text = Properties.Resources.BatchCanceling;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }
    }
}
