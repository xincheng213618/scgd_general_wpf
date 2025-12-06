using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageProjector
{
    /// <summary>
    /// ImageProjectorWindow.xaml interaction logic
    /// </summary>
    public partial class ImageProjectorWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ImageProjectorWindow));

        private FullscreenImageWindow? _fullscreenWindow;
        private BitmapImage? _currentImage;
        private Screen? _selectedScreen;
        private bool _isDisposed;

        private static ImageProjectorConfig Config => ImageProjectorConfig.Instance;
        private ObservableCollection<ImageProjectorItem> ImageItems => Config.ImageItems;

        public ImageProjectorWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LoadMonitors();
            
            // Bind ListView to ImageItems
            ImageListView.ItemsSource = ImageItems;
            
            // Initialize stretch mode ComboBox
            InitializeStretchModeComboBox();
            
            // Restore last selected index if valid
            if (Config.LastSelectedIndex >= 0 && Config.LastSelectedIndex < ImageItems.Count)
            {
                ImageListView.SelectedIndex = Config.LastSelectedIndex;
            }
            else if (ImageItems.Count > 0)
            {
                ImageListView.SelectedIndex = 0;
            }

            // Restore last selected monitor
            if (!string.IsNullOrEmpty(Config.LastSelectedMonitor))
            {
                var screens = Screen.AllScreens.ToList();
                var savedScreen = screens.FirstOrDefault(s => s.DeviceName == Config.LastSelectedMonitor);
                if (savedScreen != null)
                {
                    _selectedScreen = savedScreen;
                    MonitorLayout.SelectedScreen = _selectedScreen;
                    UpdateMonitorInfo();
                }
            }

            this.Closed += (s, e) => Dispose();
        }

        private void InitializeStretchModeComboBox()
        {
            var stretchModes = new[]
            {
                new { Value = ImageStretchMode.Uniform, Display = Properties.Resources.StretchUniform },
                new { Value = ImageStretchMode.Fill, Display = Properties.Resources.StretchFill },
                new { Value = ImageStretchMode.None, Display = Properties.Resources.StretchNone },
                new { Value = ImageStretchMode.UniformToFill, Display = Properties.Resources.StretchUniformToFill }
            };
            
            StretchModeComboBox.ItemsSource = stretchModes;
            StretchModeComboBox.DisplayMemberPath = "Display";
            StretchModeComboBox.SelectedValuePath = "Value";
            StretchModeComboBox.SelectedValue = Config.StretchMode;
        }

        private void StretchModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (StretchModeComboBox.SelectedValue is ImageStretchMode selectedMode)
            {
                Config.StretchMode = selectedMode;
                SaveConfig();
                
                // Update the fullscreen window if it's open
                if (_fullscreenWindow != null)
                {
                    _fullscreenWindow.UpdateStretch(ImageProjectorConfig.ToStretch(selectedMode));
                }
            }
        }

        private void LoadMonitors()
        {
            var screens = Screen.AllScreens.ToList();

            // Default to secondary screen, or primary if none available
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
            Config.LastSelectedMonitor = screen.DeviceName;
            UpdateMonitorInfo();
            UpdateProjectButtonState();
            SaveConfig();
        }

        private void UpdateMonitorInfo()
        {
            if (_selectedScreen != null)
            {
                var isPrimary = _selectedScreen.Primary ? Properties.Resources.PrimaryMonitor : Properties.Resources.SecondaryMonitor;
                MonitorInfoText.Text = $"{isPrimary}: {_selectedScreen.Bounds.Width} x {_selectedScreen.Bounds.Height}";
            }
        }

        private void AddImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Properties.Resources.ImageFileFilter,
                Title = Properties.Resources.SelectImageFile,
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var fileName in openFileDialog.FileNames)
                {
                    if (File.Exists(fileName) && !ImageItems.Any(item => item.FilePath == fileName))
                    {
                        ImageItems.Add(new ImageProjectorItem(fileName));
                    }
                }
                
                // Select the last added item
                if (ImageItems.Count > 0)
                {
                    ImageListView.SelectedIndex = ImageItems.Count - 1;
                }
                
                SaveConfig();
                UpdateProjectButtonState();
            }
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (ImageListView.SelectedItem is ImageProjectorItem selectedItem)
            {
                int index = ImageListView.SelectedIndex;
                ImageItems.Remove(selectedItem);
                
                // Select next available item
                if (ImageItems.Count > 0)
                {
                    ImageListView.SelectedIndex = Math.Min(index, ImageItems.Count - 1);
                }
                else
                {
                    _currentImage = null;
                    PreviewImage.Source = null;
                }
                
                SaveConfig();
                UpdateProjectButtonState();
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = ImageListView.SelectedIndex;
            if (index > 0)
            {
                var item = ImageItems[index];
                ImageItems.RemoveAt(index);
                ImageItems.Insert(index - 1, item);
                ImageListView.SelectedIndex = index - 1;
                SaveConfig();
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = ImageListView.SelectedIndex;
            if (index >= 0 && index < ImageItems.Count - 1)
            {
                var item = ImageItems[index];
                ImageItems.RemoveAt(index);
                ImageItems.Insert(index + 1, item);
                ImageListView.SelectedIndex = index + 1;
                SaveConfig();
            }
        }

        private void ImageListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ImageListView.SelectedItem is ImageProjectorItem selectedItem)
            {
                Config.LastSelectedIndex = ImageListView.SelectedIndex;
                LoadImage(selectedItem.FilePath);
                SaveConfig();
            }
            else
            {
                _currentImage = null;
                PreviewImage.Source = null;
            }
            UpdateProjectButtonState();
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

                // Create and show new fullscreen window on selected monitor with current stretch mode
                var stretch = ImageProjectorConfig.ToStretch(Config.StretchMode);
                _fullscreenWindow = new FullscreenImageWindow(_currentImage, _selectedScreen, stretch);
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
            UpdateNavigationButtonsState();
        }

        private void UpdateNavigationButtonsState()
        {
            bool isProjecting = _fullscreenWindow != null;
            int index = ImageListView.SelectedIndex;
            
            // Previous and Next buttons are enabled only when projecting and there are multiple images
            PreviousButton.IsEnabled = isProjecting && index > 0;
            NextButton.IsEnabled = isProjecting && index >= 0 && index < ImageItems.Count - 1;
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            int index = ImageListView.SelectedIndex;
            if (index > 0)
            {
                ImageListView.SelectedIndex = index - 1;
                UpdateProjectedImage();
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            int index = ImageListView.SelectedIndex;
            if (index >= 0 && index < ImageItems.Count - 1)
            {
                ImageListView.SelectedIndex = index + 1;
                UpdateProjectedImage();
            }
        }

        private void UpdateProjectedImage()
        {
            if (_fullscreenWindow != null && _currentImage != null)
            {
                _fullscreenWindow.UpdateImage(_currentImage);
                UpdateNavigationButtonsState();
            }
        }

        private void SaveConfig()
        {
            try
            {
                ConfigService.Instance.SaveConfigs();
            }
            catch (Exception ex)
            {
                log.Error("Failed to save config", ex);
            }
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
