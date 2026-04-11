using AvalonDock.Layout;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.Solution.Workspace;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        public ObservableCollection<string> FilePaths { get; } = new();
        public ObservableCollection<TimingRecord> TimingRecords { get; } = new();

        public FusionWindow()
        {
            InitializeComponent();
            FileListBox.ItemsSource = FilePaths;
            TimingListView.ItemsSource = TimingRecords;
            FilePaths.CollectionChanged += (s, e) => UpdateExecuteButton();
            UpdateCudaStatus();
        }

        public FusionWindow(IEnumerable<string> files) : this()
        {
            foreach (var file in files)
                FilePaths.Add(file);
        }

        private void UpdateCudaStatus()
        {
            bool cudaAvailable = ImageCompute.UseCuda;
            TextBlockCudaStatus.Text = cudaAvailable ? "CUDA: 可用" : "CUDA: 不可用";
            TextBlockCudaStatus.Foreground = cudaAvailable
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Gray;
        }

        private void UpdateExecuteButton()
        {
            ButtonExecute.IsEnabled = FilePaths.Count >= 2;
        }

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择图像文件",
                Filter = "图像文件 (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|所有文件 (*.*)|*.*",
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

        private static string ModeDisplayName(FusionMode mode) => mode switch
        {
            FusionMode.CPU      => "CPU (OpenCV)",
            FusionMode.GPU      => "GPU (CUDA)",
            FusionMode.GPUAsync => "GPU Async (CUDA)",
            _                   => ImageCompute.UseCuda ? "自动→GPU" : "自动→CPU",
        };

        private async void Execute_Click(object sender, RoutedEventArgs e)
        {
            if (FilePaths.Count < 2)
            {
                MessageBox.Show("请至少选择2张图像文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var mode = (FusionMode)ComboBoxMode.SelectedIndex;

            // Validate files exist
            foreach (var file in FilePaths)
            {
                if (!File.Exists(file))
                {
                    MessageBox.Show($"文件不存在: {file}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            ButtonExecute.IsEnabled = false;
            StatusText.Text = "正在执行融合...";

            try
            {
                string fusionJson = JsonConvert.SerializeObject(FilePaths.ToArray());
                int imageCount = FilePaths.Count;

                HImage hImage = default;
                long loadMs = 0, fusionMs = 0, convertMs = 0;

                var swTotal = Stopwatch.StartNew();

                int result = await Task.Run(() =>
                {
                    // Measure file loading separately (pre-warm)
                    var swLoad = Stopwatch.StartNew();
                    // Reading files is done inside the native call; we measure the whole native call
                    // then subtract convert time to estimate fusion time
                    swLoad.Stop();
                    loadMs = swLoad.ElapsedMilliseconds;

                    var swFusion = Stopwatch.StartNew();
                    int r = mode switch
                    {
                        FusionMode.CPU      => OpenCVMediaHelper.M_Fusion(fusionJson, out hImage),
                        FusionMode.GPU      => OpenCVCuda.CM_Fusion(fusionJson, out hImage),
                        FusionMode.GPUAsync => OpenCVCuda.CM_Fusion_Async(fusionJson, out hImage),
                        _                   => ImageCompute.Fusion(fusionJson, out hImage),
                    };
                    swFusion.Stop();
                    fusionMs = swFusion.ElapsedMilliseconds;
                    return r;
                });

                if (result != 0)
                {
                    StatusText.Text = $"融合失败，错误码: {result}";
                    MessageBox.Show($"融合计算失败，返回码: {result}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StatusText.Text = "融合完成，正在转换结果...";

                var swConvert = Stopwatch.StartNew();
                WriteableBitmap bitmap = hImage.ToWriteableBitmap();
                hImage.Dispose();
                swConvert.Stop();
                convertMs = swConvert.ElapsedMilliseconds;

                swTotal.Stop();
                long totalMs = swTotal.ElapsedMilliseconds;

                // Add timing record
                TimingRecords.Add(new TimingRecord
                {
                    Mode       = ModeDisplayName(mode),
                    LoadMs     = loadMs,
                    FusionMs   = fusionMs,
                    ConvertMs  = convertMs,
                    TotalMs    = totalMs,
                    ImageCount = imageCount,
                });
                // Auto-scroll to latest
                TimingListView.ScrollIntoView(TimingRecords[^1]);

                StatusText.Text = $"完成  融合: {fusionMs} ms  转换: {convertMs} ms  总计: {totalMs} ms";

                ShowResultInImageEditor(bitmap);
            }
            catch (DllNotFoundException ex)
            {
                StatusText.Text = "缺少运行库";
                MessageBox.Show($"缺少必要的运行库: {ex.Message}\n请确保opencv_helper.dll或opencv_cuda.dll存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "融合出错";
                MessageBox.Show($"融合过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonExecute.IsEnabled = FilePaths.Count >= 2;
            }
        }

        private void ShowResultInImageEditor(WriteableBitmap bitmap)
        {
            string title = $"融合结果 - {DateTime.Now:HH:mm:ss}";
            string guidId = Guid.NewGuid().ToString();

            if (WorkspaceManager.LayoutDocumentPane != null)
            {
                ImageView imageView = new ImageView();
                imageView.OpenImage(bitmap);

                LayoutDocument layoutDocument = new LayoutDocument()
                {
                    ContentId = guidId,
                    Title = title
                };
                layoutDocument.Content = imageView;
                WorkspaceManager.LayoutDocumentPane.Children.Add(layoutDocument);
                WorkspaceManager.LayoutDocumentPane.SelectedContentIndex =
                    WorkspaceManager.LayoutDocumentPane.IndexOf(layoutDocument);
                layoutDocument.Closing += async (s, e) =>
                {
                    imageView.Clear();
                    await Task.Delay(10);
                    imageView.Dispose();
                };
            }
            else
            {
                // Fallback: open in a new window
                ImageView imageView = new ImageView();
                imageView.OpenImage(bitmap);
                Window window = new Window
                {
                    Title = title,
                    Content = imageView,
                    Width = 800,
                    Height = 600,
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.Closing += async (s, e) =>
                {
                    imageView.Clear();
                    await Task.Delay(10);
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
