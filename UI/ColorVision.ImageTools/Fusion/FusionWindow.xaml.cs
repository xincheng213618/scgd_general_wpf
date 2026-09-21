#pragma warning disable CA1822,CA1863
using AvalonDock.Layout;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.Solution.Workspace;
using Microsoft.Win32;
using System.Threading;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.Solution.Fusion
{
    public enum FusionMode
    {
        Auto,
        CPU,
        GPU,
        GPUAsync
    }

    public class TimingRecord
    {
        public string Mode { get; set; } = string.Empty;
        public long LoadMs { get; set; }
        public long FusionMs { get; set; }
        public long ConvertMs { get; set; }
        public long TotalMs { get; set; }
        public int ImageCount { get; set; }
    }

    public partial class FusionWindow : Window
    {
        private CancellationTokenSource? execution;
        private bool closed;

        public ObservableCollection<string> FilePaths { get; } = new();
        public ObservableCollection<TimingRecord> TimingRecords { get; } = new();

        public FusionWindow()
        {
            InitializeComponent();
            ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
            FileListBox.ItemsSource = FilePaths;
            TimingListView.ItemsSource = TimingRecords;
            FilePaths.CollectionChanged += (s, e) => UpdateExecuteButton();
            UpdateCudaStatus();
            Closed += (_, _) => { closed = true; execution?.Cancel(); };
        }

        public FusionWindow(IEnumerable<string> files) : this()
        {
            foreach (var file in files)
                FilePaths.Add(file);
        }

        private void UpdateCudaStatus()
        {
            bool cudaAvailable = ImageCompute.UseCuda;
            TextBlockCudaStatus.Text = cudaAvailable ? Properties.Resources.Sol_Fusion_CudaAvail : Properties.Resources.Sol_Fusion_CudaUnavail;
            TextBlockCudaStatus.Foreground = cudaAvailable
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Gray;
        }

        private void UpdateExecuteButton()
        {
            ButtonExecute.IsEnabled = execution == null && !closed && FilePaths.Count >= 2;
        }

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = Properties.Resources.Sol_Fusion_ImageFiles,
                Filter = $"{Properties.Resources.Sol_Fusion_ImageFiles} (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|{Properties.Resources.Sol_Fusion_AllFiles} (*.*)|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                    FilePaths.Add(file);
            }
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = FileListBox.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected)
                FilePaths.Remove(item);
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            FilePaths.Clear();
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = FileListBox.SelectedIndex;
            if (index > 0)
            {
                FilePaths.Move(index, index - 1);
                FileListBox.SelectedIndex = index - 1;
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = FileListBox.SelectedIndex;
            if (index >= 0 && index < FilePaths.Count - 1)
            {
                FilePaths.Move(index, index + 1);
                FileListBox.SelectedIndex = index + 1;
            }
        }

        private void FileListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void FileListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
                foreach (var file in files.Where(f => imageExts.Contains(Path.GetExtension(f))).OrderBy(f => f))
                    FilePaths.Add(file);
            }
        }

        private void ClearTiming_Click(object sender, RoutedEventArgs e)
        {
            TimingRecords.Clear();
        }

        private async void Execute_Click(object sender, RoutedEventArgs e)
        {
            if (execution != null || closed) return;
            using var cancellation = new CancellationTokenSource();
            execution = cancellation;
            FileInputGroup.IsEnabled = false;
            ComboBoxMode.IsEnabled = false;
            UpdateExecuteButton();
            StatusText.Text = Properties.Resources.Sol_Fusion_Executing;
            try
            {
                FileFusionResult result = await FileFusion.Default.ExecuteAsync(FilePaths, (FileFusionMode)ComboBoxMode.SelectedIndex, cancellation.Token);
                if (closed || cancellation.IsCancellationRequested) return;
                TimingRecords.Add(new TimingRecord
                {
                    Mode = result.ActualMode.ToString(),
                    LoadMs = result.ValidationMs,
                    FusionMs = result.NativeMs,
                    ConvertMs = result.ConvertMs,
                    TotalMs = result.TotalMs,
                    ImageCount = result.Files.Count,
                });
                TimingListView.ScrollIntoView(TimingRecords[^1]);
                StatusText.Text = string.Format(Properties.Resources.Sol_Fusion_Done, $"{result.NativeMs} ms");
                ShowResultInImageEditor(result.Image);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!closed)
                {
                    StatusText.Text = Properties.Resources.Sol_Fusion_Error;
                    MessageBox.Show(this, ex.Message, Properties.Resources.Sol_Fusion_Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                execution = null;
                if (!closed)
                {
                    FileInputGroup.IsEnabled = true;
                    ComboBoxMode.IsEnabled = true;
                    UpdateExecuteButton();
                }
            }
        }
        private void ShowResultInImageEditor(BitmapSource bitmap)
        {
            string title = string.Format(Properties.Resources.Sol_Fusion_Result, DateTime.Now.ToString("HH:mm:ss"));
            string guidId = Guid.NewGuid().ToString();

            if (WorkspaceManager.LayoutDocumentPane != null)
            {
                ImageView imageView = new ImageView();
                imageView.OpenImage(new WriteableBitmap(bitmap));

                LayoutDocument layoutDocument = new LayoutDocument()
                {
                    ContentId = guidId,
                    Title = title
                };
                layoutDocument.Content = imageView;
                WorkspaceManager.LayoutDocumentPane.Children.Add(layoutDocument);
                WorkspaceManager.LayoutDocumentPane.SelectedContentIndex =
                    WorkspaceManager.LayoutDocumentPane.IndexOf(layoutDocument);
                layoutDocument.Closed += (s, e) =>
                {
                    imageView.Clear();
                    imageView.Dispose();
                };
            }
            else
            {
                // Fallback: open in a new window
                ImageView imageView = new ImageView();
                imageView.OpenImage(new WriteableBitmap(bitmap));
                Window window = new Window
                {
                    Title = title,
                    Content = imageView,
                    Width = 800,
                    Height = 600,
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(window);
                window.Closed += (s, e) =>
                {
                    imageView.Clear();
                    imageView.Dispose();
                };
                window.Show();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
