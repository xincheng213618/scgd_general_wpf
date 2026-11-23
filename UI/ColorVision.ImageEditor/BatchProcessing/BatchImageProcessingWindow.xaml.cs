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
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.BatchProcessing
{
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Each batch operation disposes its cancellation source when it finishes.")]
    public partial class BatchImageProcessingWindow : System.Windows.Window
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BatchImageProcessingWindow));
        private readonly BatchImageProcessor _processor;
        private readonly IReadOnlyList<BatchImageAlgorithmDefinition> _algorithms;
        private readonly BulkObservableCollection<BatchImageItem> _files = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private CancellationTokenSource? _folderScanCancellation;
        private bool _isClosed;
        private bool IsBusy => _cancellationTokenSource != null || _folderScanCancellation != null;

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

        public ObservableCollection<BatchImageItem> Files => _files;

        internal IReadOnlyList<BatchImageAlgorithmDefinition> Algorithms => _algorithms;

        /// <summary>Adds an already expanded input list without loading images or changing processing settings.</summary>
        public int AddPreparedInputs(IReadOnlyList<BatchImageItem> items)
        {
            Dispatcher.VerifyAccess();
            EnsureCanAddInputs();
            ArgumentNullException.ThrowIfNull(items);

            List<BatchImageItem> prepared = new(items.Count);
            foreach (BatchImageItem item in items)
            {
                ArgumentNullException.ThrowIfNull(item);
                ArgumentException.ThrowIfNullOrWhiteSpace(item.FilePath);
                string fullPath = Path.GetFullPath(item.FilePath);
                string? sourceRoot = null;
                if (item.SourceRoot != null)
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(item.SourceRoot);
                    sourceRoot = Path.GetFullPath(item.SourceRoot);
                    string relativePath = Path.GetRelativePath(sourceRoot, fullPath);
                    if (relativePath == "." || relativePath == ".." || Path.IsPathRooted(relativePath)
                        || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("Each prepared input must be inside its source root.", nameof(items));
                    }
                }
                if (!IsSupportedFile(fullPath))
                {
                    throw new NotSupportedException(string.Format(Properties.Resources.BatchUnsupportedFormat, Path.GetExtension(fullPath)));
                }
                prepared.Add(new BatchImageItem(fullPath, sourceRoot));
            }

            return AddUniqueInputs(prepared);
        }

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
            if (IsBusy) return;
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

            AddUniqueInputs(dialog.FileNames.Where(IsSupportedFile).OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new BatchImageItem(Path.GetFullPath(path))));
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy) return;
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
                await AddFolderInputsAsync(dialog.SelectedPath, RecursiveCheckBox.IsChecked == true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                if (!_isClosed)
                    MessageBox.Show(this, ex.Message, Properties.Resources.BatchAddFolderFailed, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal async Task<int> AddFolderInputsAsync(string folderPath, bool includeSubfolders)
        {
            Dispatcher.VerifyAccess();
            EnsureCanAddInputs();
            string root = Path.GetFullPath(folderPath);
            HashSet<string> extensions = _processor.SupportedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
            using var cancellation = new CancellationTokenSource();
            _folderScanCancellation = cancellation;
            string previousProgressText = ProgressTextBlock.Text;
            SetBusyState(true);
            ProgressBar.IsIndeterminate = true;
            ProgressTextBlock.Text = Properties.Resources.BatchScanningFolder;
            try
            {
                await Dispatcher.Yield(DispatcherPriority.Background);
                BatchImageItem[] inputs = await Task.Run(
                    () => ScanFolderInputs(root, includeSubfolders, extensions, cancellation.Token), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                return AddUniqueInputs(inputs);
            }
            finally
            {
                _folderScanCancellation = null;
                if (!_isClosed)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressTextBlock.Text = previousProgressText;
                    SetBusyState(false);
                }
            }
        }

        private static BatchImageItem[] ScanFolderInputs(string root, bool includeSubfolders, HashSet<string> extensions, CancellationToken cancellationToken)
        {
            var directories = new Stack<string>();
            directories.Push(root);
            var paths = new List<string>();
            var extensionLookup = extensions.GetAlternateLookup<ReadOnlySpan<char>>();
            // Match DirectoryInfo.EnumerateFileSystemInfos, including hidden entries and access errors.
            var options = new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false };
            FileSystemEnumerable<(string Path, bool IsDirectory)>.FindPredicate include = ShouldInclude;
            while (directories.TryPop(out string? directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entries = new FileSystemEnumerable<(string Path, bool IsDirectory)>(directory,
                    static (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.IsDirectory), options)
                {
                    ShouldIncludePredicate = include,
                };
                foreach (var entry in entries)
                {
                    if (entry.IsDirectory) directories.Push(entry.Path);
                    else paths.Add(entry.Path);
                }
            }
            return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(path => new BatchImageItem(path, root)).ToArray();

            bool ShouldInclude(ref FileSystemEntry entry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.IsDirectory)
                    return includeSubfolders && (entry.Attributes & FileAttributes.ReparsePoint) == 0;

                // FileSystemInfo.Extension preserves a trailing dot in extended-length paths.
                ReadOnlySpan<char> fileName = entry.FileName;
                int dot = fileName.LastIndexOf('.');
                return extensionLookup.Contains(dot < 0 ? ReadOnlySpan<char>.Empty : fileName[dot..]);
            }
        }

        private void EnsureCanAddInputs()
        {
            if (_isClosed || IsBusy)
                throw new InvalidOperationException("Batch inputs cannot be added during a batch operation or after the window is closed.");
        }

        private int AddUniqueInputs(IEnumerable<BatchImageItem> inputs)
        {
            HashSet<string> existing = _files.Select(item => item.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            BatchImageItem[] added = inputs.Where(item => existing.Add(item.FilePath)).ToArray();
            _files.AddRange(added);
            UpdateFileCount();
            return added.Length;
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
            if (IsBusy) return;
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

        private void SetBusyState(bool isBusy)
        {
            SettingsPanel.IsEnabled = !isBusy;
            AddFilesButton.IsEnabled = !isBusy;
            AddFolderButton.IsEnabled = !isBusy;
            RemoveButton.IsEnabled = !isBusy;
            ClearButton.IsEnabled = !isBusy;
            RecursiveCheckBox.IsEnabled = !isBusy;
            ExecuteButton.IsEnabled = !isBusy;
            CancelButton.IsEnabled = isBusy;
        }

        private void SetProcessingState(bool isProcessing, int total)
        {
            SetBusyState(isProcessing);
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
            (_cancellationTokenSource ?? _folderScanCancellation)?.Cancel();
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

            _folderScanCancellation?.Cancel();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            base.OnClosed(e);
        }
    }
}
