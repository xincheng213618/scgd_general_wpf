using ColorVision.Themes;
using ColorVision.UI.Marketplace;
using log4net;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.Feedback
{
    public class AttachmentItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string SizeText => File.Exists(FilePath)
            ? $"({new FileInfo(FilePath).Length / 1024.0:F1} KB)"
            : "(pending)";
    }

    /// <summary>
    /// Represents a selectable IFeedbackLogCollector shown in the diagnostic items list.
    /// </summary>
    public class CollectorItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public IFeedbackLogCollector Collector { get; }
        public string Name => Collector.Name;
        public string Description { get; set; } = string.Empty;

        public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged(); } }
        private bool _isChecked = true;

        public CollectorItem(IFeedbackLogCollector collector)
        {
            Collector = collector;
        }
    }

    /// <summary>
    /// HttpContent wrapper that reports upload progress via IProgress.
    /// </summary>
    internal class ProgressableStreamContent : HttpContent
    {
        private readonly HttpContent _innerContent;
        private readonly IProgress<double> _progress;

        public ProgressableStreamContent(HttpContent innerContent, IProgress<double> progress)
        {
            _innerContent = innerContent;
            foreach (var header in _innerContent.Headers)
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            _progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            var buffer = new byte[81920];
            long totalLength = _innerContent.Headers.ContentLength ?? -1;
            long totalRead = 0;

            using var innerStream = await _innerContent.ReadAsStreamAsync();
            int bytesRead;
            while ((bytesRead = await innerStream.ReadAsync(buffer)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (totalLength > 0)
                    _progress.Report((double)totalRead / totalLength * 100);
            }

            _progress.Report(100);
        }

        protected override bool TryComputeLength(out long length)
        {
            var contentLength = _innerContent.Headers.ContentLength;
            length = contentLength ?? -1;
            return contentLength.HasValue;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _innerContent.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// FeedbackWindow — allows users to send feedback with log files, screenshots, and attachments.
    /// </summary>
    public partial class FeedbackWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FeedbackWindow));

        private readonly ObservableCollection<AttachmentItem> _attachments = new();
        private readonly ObservableCollection<CollectorItem> _collectorItems = new();
        private bool _placeholderActive = true;

        public FeedbackWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            AttachmentsList.ItemsSource = _attachments;
            CollectorsList.ItemsSource = _collectorItems;

            // Discover all IFeedbackLogCollector implementations and show them in the list
            try
            {
                var collectors = AssemblyHandler.GetInstance().LoadImplementations<IFeedbackLogCollector>();
                collectors.Sort((a, b) => a.Order.CompareTo(b.Order));
                foreach (var collector in collectors)
                {
                    _collectorItems.Add(new CollectorItem(collector));
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Failed to discover collectors: {ex.Message}");
            }
        }

        private void MessageTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_placeholderActive)
            {
                _placeholderActive = false;
                MessageTextBox.Text = string.Empty;
            }
        }

        private void AddFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = Properties.Resources.AddFile,
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    _attachments.Add(new AttachmentItem { FilePath = file });
                }
            }
        }

        private async void CaptureScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Minimize this window, capture screen, then restore
                this.WindowState = WindowState.Minimized;
                await Task.Delay(300);

                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                if (screen == null) return;

                var bounds = screen.Bounds;
                using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
                }

                string tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bitmap.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

                _attachments.Add(new AttachmentItem { FilePath = tempPath });
                StatusText.Text = Properties.Resources.ScreenshotCaptured;

                this.WindowState = WindowState.Normal;
            }
            catch (Exception ex)
            {
                this.WindowState = WindowState.Normal;
                log.Error($"Screenshot capture failed: {ex.Message}");
                StatusText.Text = string.Format(Properties.Resources.SendFailed, ex.Message);
            }
        }

        private void PackLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get only the checked collectors
                var selectedCollectors = _collectorItems.Where(c => c.IsChecked).Select(c => c.Collector).ToList();

                if (selectedCollectors.Count == 0)
                {
                    StatusText.Text = Properties.Resources.NoLocalLog4Output;
                    return;
                }

                string zipPath = Path.Combine(Path.GetTempPath(), $"ColorVision_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                var tempFiles = new List<string>();
                int totalFiles = 0;

                using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var collector in selectedCollectors)
                    {
                        try
                        {
                            StatusText.Text = string.Format(Properties.Resources.CollectingLogs, collector.Name);

                            foreach (var (entryPath, filePath) in collector.CollectFiles())
                            {
                                try
                                {
                                    if (File.Exists(filePath))
                                    {
                                        zipArchive.CreateEntryFromFile(filePath, entryPath);
                                        tempFiles.Add(filePath);
                                        totalFiles++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    log.Debug($"Could not add {entryPath} from {collector.Name}: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Debug($"Collector '{collector.Name}' failed: {ex.Message}");
                        }
                    }
                }

                // Clean up temp files
                foreach (var tempFile in tempFiles)
                {
                    try { File.Delete(tempFile); } catch { }
                }

                if (totalFiles == 0)
                {
                    StatusText.Text = Properties.Resources.NoLocalLog4Output;
                    try { File.Delete(zipPath); } catch { }
                    return;
                }

                _attachments.Add(new AttachmentItem { FilePath = zipPath });
                StatusText.Text = string.Format(Properties.Resources.LogsPackaged, Path.GetFileName(zipPath));
            }
            catch (Exception ex)
            {
                log.Error($"PackLogs failed: {ex.Message}");
                StatusText.Text = string.Format(Properties.Resources.SendFailed, ex.Message);
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AttachmentItem item)
            {
                _attachments.Remove(item);
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            string message = _placeholderActive ? string.Empty : MessageTextBox.Text.Trim();

            if (string.IsNullOrEmpty(message) && _attachments.Count == 0)
            {
                MessageBox.Show(this, Properties.Resources.FeedbackEmptyWarning, Properties.Resources.SendFeedback, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SendButton.IsEnabled = false;
            PackLogsButton.IsEnabled = false;
            AddFileButton.IsEnabled = false;
            AddScreenshotButton.IsEnabled = false;

            UploadProgressBar.Value = 0;
            UploadProgressBar.Visibility = Visibility.Visible;
            StatusText.Text = Properties.Resources.Sending + "...";

            try
            {
                string baseUrl = MarketplaceConfig.Instance.MarketplaceApiUrl?.TrimEnd('/') ?? string.Empty;
                if (string.IsNullOrEmpty(baseUrl))
                {
                    string legacy = UI.Plugins.PluginLoaderrConfig.Instance.PluginUpdatePath ?? string.Empty;
                    if (!string.IsNullOrEmpty(legacy))
                    {
                        try
                        {
                            var uri = new Uri(legacy);
                            baseUrl = $"{uri.Scheme}://{uri.Authority}";
                        }
                        catch { }
                    }
                }

                if (string.IsNullOrEmpty(baseUrl))
                {
                    StatusText.Text = string.Format(Properties.Resources.SendFailed, Properties.Resources.ServerAddressNotConfigured);
                    return;
                }

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                using var form = new MultipartFormDataContent();

                form.Add(new StringContent(message), "message");
                form.Add(new StringContent(Environment.UserName), "userName");
                form.Add(new StringContent(typeof(FeedbackWindow).Assembly.GetName().Version?.ToString() ?? ""), "appVersion");
                form.Add(new StringContent($"{Environment.MachineName} / {Environment.OSVersion}"), "machineInfo");

                foreach (var attachment in _attachments)
                {
                    if (File.Exists(attachment.FilePath))
                    {
                        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(attachment.FilePath));
                        form.Add(fileContent, "files", attachment.FileName);
                    }
                }

                // Wrap form content with progress reporting
                var progress = new Progress<double>(percent =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        UploadProgressBar.Value = percent;
                        StatusText.Text = string.Format(Properties.Resources.Uploading, (int)percent);
                    });
                });

                var progressContent = new ProgressableStreamContent(form, progress);

                var response = await httpClient.PostAsync($"{baseUrl}/api/feedback", progressContent);
                if (response.IsSuccessStatusCode)
                {
                    UploadProgressBar.Value = 100;
                    StatusText.Text = Properties.Resources.FeedbackSent;
                    MessageBox.Show(this, Properties.Resources.FeedbackSent, Properties.Resources.SendFeedback, MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
                else
                {
                    string body = await response.Content.ReadAsStringAsync();
                    StatusText.Text = string.Format(Properties.Resources.SendFailed, response.StatusCode);
                    log.Error($"Feedback send failed: {response.StatusCode} {body}");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Properties.Resources.SendFailed, ex.Message);
                log.Error($"Feedback send failed: {ex.Message}");
            }
            finally
            {
                SendButton.IsEnabled = true;
                PackLogsButton.IsEnabled = true;
                AddFileButton.IsEnabled = true;
                AddScreenshotButton.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
