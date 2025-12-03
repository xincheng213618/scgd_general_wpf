using ColorVision.Themes;
using log4net;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pattern.ImageProjector
{
    /// <summary>
    /// ImageProjectorWindow. xaml interaction logic
    /// </summary>
    public partial class ImageProjectorWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ImageProjectorWindow));

        private FullscreenImageWindow? _fullscreenWindow;
        private BitmapImage? _currentImage;
        private Screen? _selectedScreen;
        private bool _isDisposed;

        public ImageProjectorWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LoadMonitors();
            this.Closed += (s, e) => Dispose();
        }

        private void LoadMonitors()
        {
            var screens = Screen.AllScreens.ToList();

            // 设置默认选择的屏幕（优先选择非主显示器）
            _selectedScreen = screens.FirstOrDefault(s => !s.Primary) ?? screens.FirstOrDefault();

            if (_selectedScreen != null)
            {
                MonitorLayout.SelectedScreen = _selectedScreen;
            }

            UpdateMonitorInfo();
        }

        private void MonitorLayout_ScreenSelected(object? sender, Screen screen)
        {
            _selectedScreen = screen;
            UpdateMonitorInfo();
            UpdateProjectButtonState();
        }

        private void UpdateMonitorInfo()
        {
            if (_selectedScreen != null)
            {
                var isPrimary = _selectedScreen.Primary ? Properties.Resources.PrimaryMonitor : Properties.Resources.SecondaryMonitor;
                MonitorInfoText.Text = $"{isPrimary}: {_selectedScreen.Bounds.Width} x {_selectedScreen.Bounds.Height}";
            }
        }

        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Properties.Resources.ImageFileFilter,
                Title = Properties.Resources.SelectImageFile
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadImage(openFileDialog.FileName);
            }
        }

        private void LoadImage(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    System.Windows.MessageBox.Show(Properties.Resources.FileNotFound, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                _currentImage = bitmap;
                PreviewImage.Source = _currentImage;
                ImagePathTextBox.Text = filePath;

                UpdateProjectButtonState();
                StatusText.Text = $"{Properties.Resources.ImageLoaded}: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                log.Error($"Failed to load image: {filePath}", ex);
                System.Windows.MessageBox.Show(
                    $"{Properties.Resources.FailedToLoadImage}: {ex.Message}",
                    Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateProjectButtonState()
        {
            ProjectButton.IsEnabled = _currentImage != null && _selectedScreen != null;
        }

        private void Project_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage == null || _selectedScreen == null)
            {
                System.Windows.MessageBox.Show(Properties.Resources.SelectImageFirst, Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Close existing fullscreen window if any
                CloseFullscreenWindow();

                // Create and show new fullscreen window on selected monitor
                _fullscreenWindow = new FullscreenImageWindow(_currentImage, _selectedScreen);
                _fullscreenWindow.Closed += (s, args) =>
                {
                    _fullscreenWindow = null;
                    UpdateStopButtonState();
                    StatusText.Text = Properties.Resources.ProjectionStopped;
                };
                _fullscreenWindow.Show();

                UpdateStopButtonState();
                StatusText.Text = Properties.Resources.ProjectionStarted;
            }
            catch (Exception ex)
            {
                log.Error("Failed to start projection", ex);
                System.Windows.MessageBox.Show(
                    $"{Properties.Resources.FailedToStartProjection}: {ex.Message}",
                    Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            CloseFullscreenWindow();
            StatusText.Text = Properties.Resources.ProjectionStopped;
        }

        private void CloseFullscreenWindow()
        {
            if (_fullscreenWindow != null)
            {
                _fullscreenWindow.Close();
                _fullscreenWindow = null;
            }
            UpdateStopButtonState();
        }

        private void UpdateStopButtonState()
        {
            StopButton.IsEnabled = _fullscreenWindow != null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            GC.SuppressFinalize(this);
            CloseFullscreenWindow();
        }
    }
}