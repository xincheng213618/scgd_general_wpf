using ColorVision.Themes;
using ColorVision.UI.Marketplace;
using log4net;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
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
    /// FeedbackWindow — allows users to send feedback with log files, screenshots, and attachments.
    /// </summary>
    public partial class FeedbackWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FeedbackWindow));

        private readonly ObservableCollection<AttachmentItem> _attachments = new();
        private bool _placeholderActive = true;

        public FeedbackWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            AttachmentsList.ItemsSource = _attachments;
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
                StatusText.Text = $"截图失败: {ex.Message}";
            }
        }

        private void PackLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Discover all IFeedbackLogCollector implementations from all loaded assemblies
                var collectors = AssemblyHandler.GetInstance().LoadImplementations<IFeedbackLogCollector>();

                if (collectors.Count == 0)
                {
                    StatusText.Text = Properties.Resources.NoLocalLog4Output;
                    return;
                }

                // Sort by order
                collectors.Sort((a, b) => a.Order.CompareTo(b.Order));

                string zipPath = Path.Combine(Path.GetTempPath(), $"ColorVision_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                var tempFiles = new List<string>();
                int totalFiles = 0;

                using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var collector in collectors)
                    {
                        try
                        {
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
                StatusText.Text = $"打包日志失败: {ex.Message}";
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
                    StatusText.Text = "无法确定服务器地址";
                    SendButton.IsEnabled = true;
                    return;
                }

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
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

                var response = await httpClient.PostAsync($"{baseUrl}/api/feedback", form);
                if (response.IsSuccessStatusCode)
                {
                    StatusText.Text = Properties.Resources.FeedbackSent;
                    MessageBox.Show(this, Properties.Resources.FeedbackSent, Properties.Resources.SendFeedback, MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
                else
                {
                    string body = await response.Content.ReadAsStringAsync();
                    StatusText.Text = $"发送失败: {response.StatusCode}";
                    log.Error($"Feedback send failed: {response.StatusCode} {body}");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"发送失败: {ex.Message}";
                log.Error($"Feedback send failed: {ex.Message}");
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
