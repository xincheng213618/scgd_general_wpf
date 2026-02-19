using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using log4net;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Video
{
    [FileExtension(".mp4|.avi|.mkv|.mov|.wmv|.flv|.webm")]
    public record class VideoOpen(EditorContext EditorContext) : IImageOpen
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VideoOpen));

        private int _videoHandle = -1;
        private OpenCVMediaHelper.VideoInfo _videoInfo;
        private WriteableBitmap? _writeableBitmap;
        private bool _isPlaying;
        private Slider? _progressSlider;
        private Button? _playPauseButton;
        private Button? _stopButton;
        private ComboBox? _speedComboBox;
        private TextBlock? _timeTextBlock;
        private ToolBar? _videoToolBar;
        private bool _isDragging;
        private ImageView? _imageView;

        // Must keep delegate references alive to prevent GC collection during callbacks
        private OpenCVMediaHelper.VideoFrameCallback? _frameCallbackDelegate;
        private OpenCVMediaHelper.VideoStatusCallback? _statusCallbackDelegate;

        public void OpenImage(EditorContext context, string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // Close any previous video
            CloseVideo();

            FileInfo fileInfo = new FileInfo(filePath);
            context.Config.AddProperties("FileSource", filePath);
            context.Config.AddProperties("FileName", fileInfo.Name);
            context.Config.AddProperties("FileSize", fileInfo.Length);

            // Open video via native opencv_helper
            int handle = OpenCVMediaHelper.M_VideoOpen(filePath, out var info);
            if (handle <= 0)
            {
                MessageBox.Show($"Failed to open video file: {filePath}");
                return;
            }

            _videoHandle = handle;
            _videoInfo = info;
            _imageView = context.ImageView;

            context.Config.AddProperties("VideoWidth", info.width);
            context.Config.AddProperties("VideoHeight", info.height);
            context.Config.AddProperties("VideoFPS", info.fps);
            context.Config.AddProperties("VideoTotalFrames", info.totalFrames);
            double fps = info.fps > 0 ? info.fps : 30.0;
            context.Config.AddProperties("VideoDuration", TimeSpan.FromSeconds(info.totalFrames / fps).ToString(@"hh\:mm\:ss"));

            // Read first frame and display
            int ret = OpenCVMediaHelper.M_VideoReadFrame(handle, out HImage firstFrame);
            if (ret == 0)
            {
                _writeableBitmap = firstFrame.ToWriteableBitmap();
                firstFrame.Dispose();
                context.ImageView.SetImageSource(_writeableBitmap);
                context.ImageView.UpdateZoomAndScale();
            }

            // Seek back to start
            OpenCVMediaHelper.M_VideoSeek(handle, 0);

            // Setup video playback controls in bottom toolbar
            SetupVideoControls(context);
        }

        private void SetupVideoControls(EditorContext context)
        {
            var imageView = context.ImageView;
            _videoToolBar = imageView.ToolBarAl;

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Play/Pause button
                _playPauseButton = new Button
                {
                    Content = "▶",
                    Width = 30,
                    Height = 24,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 12,
                    ToolTip = "Play/Pause"
                };
                _playPauseButton.Click += PlayPauseButton_Click;

                // Progress slider
                _progressSlider = new Slider
                {
                    Minimum = 0,
                    Maximum = _videoInfo.totalFrames > 0 ? _videoInfo.totalFrames - 1 : 0,
                    Value = 0,
                    Width = 300,
                    Height = 20,
                    Margin = new Thickness(5, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "Seek"
                };
                _progressSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(Slider_DragStarted));
                _progressSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Slider_DragCompleted));

                double displayFps = _videoInfo.fps > 0 ? _videoInfo.fps : 30.0;
                // Time display
                _timeTextBlock = new TextBlock
                {
                    Text = "00:00:00 / " + TimeSpan.FromSeconds(_videoInfo.totalFrames / displayFps).ToString(@"hh\:mm\:ss"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 11
                };

                // Speed selector
                _speedComboBox = new ComboBox
                {
                    Width = 60,
                    Height = 24,
                    Margin = new Thickness(5, 0, 0, 0),
                    ToolTip = "Playback Speed"
                };
                _speedComboBox.Items.Add(new ComboBoxItem { Content = "0.25x", Tag = 0.25 });
                _speedComboBox.Items.Add(new ComboBoxItem { Content = "0.5x", Tag = 0.5 });
                _speedComboBox.Items.Add(new ComboBoxItem { Content = "1x", Tag = 1.0 });
                _speedComboBox.Items.Add(new ComboBoxItem { Content = "1.5x", Tag = 1.5 });
                _speedComboBox.Items.Add(new ComboBoxItem { Content = "2x", Tag = 2.0 });
                _speedComboBox.Items.Add(new ComboBoxItem { Content = "4x", Tag = 4.0 });
                _speedComboBox.SelectedIndex = 2; // Default 1x
                _speedComboBox.SelectionChanged += SpeedComboBox_SelectionChanged;

                // Stop button
                _stopButton = new Button
                {
                    Content = "■",
                    Width = 30,
                    Height = 24,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 12,
                    ToolTip = "Stop"
                };
                _stopButton.Click += StopButton_Click;

                _videoToolBar.Items.Add(_playPauseButton);
                _videoToolBar.Items.Add(_stopButton);
                _videoToolBar.Items.Add(_progressSlider);
                _videoToolBar.Items.Add(_timeTextBlock);
                _videoToolBar.Items.Add(_speedComboBox);

                _videoToolBar.Visibility = Visibility.Visible;
            });

            // Subscribe to clear event to cleanup
            imageView.ClearImageEventHandler += OnImageCleared;
        }

        private void OnImageCleared(object? sender, EventArgs e)
        {
            CloseVideo();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_videoHandle <= 0) return;

            if (_isPlaying)
            {
                PauseVideo();
            }
            else
            {
                PlayVideo();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_videoHandle <= 0) return;

            PauseVideo();
            OpenCVMediaHelper.M_VideoSeek(_videoHandle, 0);
            UpdateSliderPosition(0);

            // Read and display first frame
            int ret = OpenCVMediaHelper.M_VideoReadFrame(_videoHandle, out HImage firstFrame);
            if (ret == 0)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    UpdateFrameDisplay(firstFrame);
                });
            }
            OpenCVMediaHelper.M_VideoSeek(_videoHandle, 0);
        }

        private void PlayVideo()
        {
            if (_videoHandle <= 0 || _isPlaying) return;

            _isPlaying = true;
            UpdatePlayPauseButton(true);

            _frameCallbackDelegate = OnFrameReceived;
            _statusCallbackDelegate = OnStatusChanged;

            OpenCVMediaHelper.M_VideoPlay(_videoHandle, _frameCallbackDelegate, _statusCallbackDelegate, IntPtr.Zero);
        }

        private void PauseVideo()
        {
            if (_videoHandle <= 0 || !_isPlaying) return;

            OpenCVMediaHelper.M_VideoPause(_videoHandle);
            _isPlaying = false;
            UpdatePlayPauseButton(false);
        }

        private void OnFrameReceived(int handle, ref HImage frame, int currentFrame, int totalFrames, IntPtr userData)
        {
            try
            {
                HImage localFrame = frame;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (_videoHandle <= 0) return;

                    UpdateFrameDisplay(localFrame);

                    if (!_isDragging)
                    {
                        UpdateSliderPosition(currentFrame);
                    }

                    UpdateTimeDisplay(currentFrame);
                });
            }
            catch (Exception ex)
            {
                log.Error("Error in video frame callback", ex);
            }
        }

        private void UpdateFrameDisplay(HImage frame)
        {
            if (_writeableBitmap != null &&
                _writeableBitmap.PixelWidth == frame.cols &&
                _writeableBitmap.PixelHeight == frame.rows)
            {
                // Reuse existing WriteableBitmap for performance
                if (!HImageExtension.UpdateWriteableBitmap(_writeableBitmap, frame))
                {
                    // UpdateWriteableBitmap failed (format mismatch), create new
                    var newBitmap = frame.ToWriteableBitmap();
                    frame.Dispose();
                    _writeableBitmap = newBitmap;
                    if (_imageView?.ImageShow != null)
                        _imageView.ImageShow.Source = _writeableBitmap;
                }
                // Note: UpdateWriteableBitmap already disposes frame on success
            }
            else
            {
                _writeableBitmap = frame.ToWriteableBitmap();
                frame.Dispose();
                if (_imageView?.ImageShow != null)
                    _imageView.ImageShow.Source = _writeableBitmap;
            }
        }

        private void OnStatusChanged(int handle, int status, IntPtr userData)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case 0: // Paused
                        _isPlaying = false;
                        UpdatePlayPauseButton(false);
                        break;
                    case 1: // Playing
                        _isPlaying = true;
                        UpdatePlayPauseButton(true);
                        break;
                    case 2: // Ended
                        _isPlaying = false;
                        UpdatePlayPauseButton(false);
                        // Reset to beginning
                        if (_videoHandle > 0)
                        {
                            OpenCVMediaHelper.M_VideoSeek(_videoHandle, 0);
                            UpdateSliderPosition(0);
                        }
                        break;
                }
            });
        }

        private void UpdatePlayPauseButton(bool isPlaying)
        {
            if (_playPauseButton == null) return;
            if (_playPauseButton.Dispatcher.CheckAccess())
            {
                _playPauseButton.Content = isPlaying ? "⏸" : "▶";
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _playPauseButton.Content = isPlaying ? "⏸" : "▶";
                });
            }
        }

        private void UpdateSliderPosition(int frameIndex)
        {
            if (_progressSlider == null || _isDragging) return;
            if (_progressSlider.Dispatcher.CheckAccess())
            {
                _progressSlider.Value = frameIndex;
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _progressSlider.Value = frameIndex;
                });
            }
        }

        private void UpdateTimeDisplay(int currentFrame)
        {
            if (_timeTextBlock == null) return;
            double fps = _videoInfo.fps > 0 ? _videoInfo.fps : 30.0;
            var current = TimeSpan.FromSeconds(currentFrame / fps);
            var total = TimeSpan.FromSeconds(_videoInfo.totalFrames / fps);
            if (_timeTextBlock.Dispatcher.CheckAccess())
            {
                _timeTextBlock.Text = $"{current:hh\\:mm\\:ss} / {total:hh\\:mm\\:ss}";
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _timeTextBlock.Text = $"{current:hh\\:mm\\:ss} / {total:hh\\:mm\\:ss}";
                });
            }
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
            if (_isPlaying)
            {
                PauseVideo();
            }
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            if (_videoHandle <= 0 || _progressSlider == null) return;

            int targetFrame = (int)_progressSlider.Value;
            OpenCVMediaHelper.M_VideoSeek(_videoHandle, targetFrame);

            // Read and display the frame at the seek position
            int ret = OpenCVMediaHelper.M_VideoReadFrame(_videoHandle, out HImage seekFrame);
            if (ret == 0)
            {
                UpdateFrameDisplay(seekFrame);
            }

            UpdateTimeDisplay(targetFrame);
        }

        private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_videoHandle <= 0 || _speedComboBox == null) return;

            if (_speedComboBox.SelectedItem is ComboBoxItem item && item.Tag is double speed)
            {
                OpenCVMediaHelper.M_VideoSetPlaybackSpeed(_videoHandle, speed);
            }
        }

        private void CloseVideo()
        {
            if (_videoHandle > 0)
            {
                if (_isPlaying)
                {
                    OpenCVMediaHelper.M_VideoPause(_videoHandle);
                    _isPlaying = false;
                }

                OpenCVMediaHelper.M_VideoClose(_videoHandle);
                _videoHandle = -1;
            }

            // Clean up UI controls
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_videoToolBar != null)
                {
                    // Remove video-specific controls (keep original items like ComboxPOITemplate)
                    if (_playPauseButton != null && _videoToolBar.Items.Contains(_playPauseButton))
                        _videoToolBar.Items.Remove(_playPauseButton);
                    if (_stopButton != null && _videoToolBar.Items.Contains(_stopButton))
                        _videoToolBar.Items.Remove(_stopButton);
                    if (_progressSlider != null && _videoToolBar.Items.Contains(_progressSlider))
                        _videoToolBar.Items.Remove(_progressSlider);
                    if (_timeTextBlock != null && _videoToolBar.Items.Contains(_timeTextBlock))
                        _videoToolBar.Items.Remove(_timeTextBlock);
                    if (_speedComboBox != null && _videoToolBar.Items.Contains(_speedComboBox))
                        _videoToolBar.Items.Remove(_speedComboBox);
                }
            });

            if (_imageView != null)
            {
                _imageView.ClearImageEventHandler -= OnImageCleared;
            }

            _frameCallbackDelegate = null;
            _statusCallbackDelegate = null;
            _writeableBitmap = null;
            _imageView = null;
        }
    }
}
