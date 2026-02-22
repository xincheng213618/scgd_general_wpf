using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using log4net;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
        private Button? _muteButton;
        private ComboBox? _speedComboBox;
        private ComboBox? _resizeComboBox;
        private TextBlock? _timeTextBlock;
        private TextBlock? _frameInfoTextBlock;
        private ToolBar? _videoToolBar;
        private bool _isDragging;
        private ImageView? _imageView;

        // Audio playback via WPF MediaPlayer (handles audio from the same video file)
        private MediaPlayer? _mediaPlayer;
        private string? _currentFilePath;
        private double _currentSpeed = 1.0;
        private bool _isMuted;

        // Must keep delegate references alive to prevent GC collection during callbacks
        private OpenCVMediaHelper.VideoFrameCallback? _frameCallbackDelegate;
        private OpenCVMediaHelper.VideoStatusCallback? _statusCallbackDelegate;

        // Frame dropping and UI throttling for high-resolution video (e.g. 8K@60fps)
        private int _isProcessingFrame; // 0 = idle, 1 = processing (atomic via Interlocked)
        private int _uiUpdateCounter;   // throttle slider/time updates

        // Auto-hide toolbar
        private DispatcherTimer? _mouseIdleTimer;
        private bool _autoHideEnabled = true;
        private CheckBox? _autoHideCheckBox;
        private DateTime _lastMouseMoveTime = DateTime.Now;
        private const double MouseIdleTimeoutSeconds = 3.0;
        private const double AudioSyncDriftThresholdSeconds = 0.5;

        // Audio sync drift correction
        private int _droppedFrameCount;
        private int _lastSyncFrame;

        // Current resize scale
        private double _currentResizeScale = 1.0;

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
            _currentFilePath = filePath;

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
                // Initialize audio player on UI thread (MediaPlayer requires STA)
                InitAudioPlayer();

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

                // Progress slider - supports both drag and click-to-seek
                _progressSlider = new Slider
                {
                    Minimum = 0,
                    Maximum = _videoInfo.totalFrames > 0 ? _videoInfo.totalFrames - 1 : 0,
                    Value = 0,
                    Width = 300,
                    Height = 20,
                    Margin = new Thickness(5, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsMoveToPointEnabled = true,
                    ToolTip = "Seek (click or drag)"
                };
                _progressSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(Slider_DragStarted));
                _progressSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Slider_DragCompleted));
                _progressSlider.PreviewMouseLeftButtonUp += ProgressSlider_PreviewMouseLeftButtonUp;

                double displayFps = _videoInfo.fps > 0 ? _videoInfo.fps : 30.0;
                // Time display
                _timeTextBlock = new TextBlock
                {
                    Text = "00:00:00 / " + TimeSpan.FromSeconds(_videoInfo.totalFrames / displayFps).ToString(@"hh\:mm\:ss"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 11
                };

                // Frame info display (shows original source dimensions)
                _frameInfoTextBlock = new TextBlock
                {
                    Text = $"0/{_videoInfo.totalFrames} [{_videoInfo.width}x{_videoInfo.height}]",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 10,
                    Opacity = 0.7
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

                // Resize scale selector
                _resizeComboBox = new ComboBox
                {
                    Width = 55,
                    Height = 24,
                    Margin = new Thickness(5, 0, 0, 0),
                    ToolTip = "Display Scale (reduce for large videos)"
                };
                _resizeComboBox.Items.Add(new ComboBoxItem { Content = "1x", Tag = 1.0 });
                _resizeComboBox.Items.Add(new ComboBoxItem { Content = "1/2", Tag = 0.5 });
                _resizeComboBox.Items.Add(new ComboBoxItem { Content = "1/4", Tag = 0.25 });
                _resizeComboBox.Items.Add(new ComboBoxItem { Content = "1/8", Tag = 0.125 });
                _resizeComboBox.SelectedIndex = 0; // Default 1x
                _resizeComboBox.SelectionChanged += ResizeComboBox_SelectionChanged;

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

                // Mute/Unmute button
                _muteButton = new Button
                {
                    Content = "🔊",
                    Width = 30,
                    Height = 24,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 12,
                    ToolTip = "Mute/Unmute"
                };
                _muteButton.Click += MuteButton_Click;

                // Auto-hide toggle
                _autoHideCheckBox = new CheckBox
                {
                    Content = "Auto Hide",
                    IsChecked = _autoHideEnabled,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 10,
                    ToolTip = "Auto-hide controls when mouse is idle"
                };
                _autoHideCheckBox.Checked += (s, e) => _autoHideEnabled = true;
                _autoHideCheckBox.Unchecked += (s, e) =>
                {
                    _autoHideEnabled = false;
                    if (_videoToolBar != null)
                        _videoToolBar.Opacity = 0.8;
                };

                _videoToolBar.Items.Add(_playPauseButton);
                _videoToolBar.Items.Add(_stopButton);
                _videoToolBar.Items.Add(_muteButton);
                _videoToolBar.Items.Add(_progressSlider);
                _videoToolBar.Items.Add(_timeTextBlock);
                _videoToolBar.Items.Add(_frameInfoTextBlock);
                _videoToolBar.Items.Add(_speedComboBox);
                _videoToolBar.Items.Add(_resizeComboBox);
                _videoToolBar.Items.Add(_autoHideCheckBox);

                _videoToolBar.Visibility = Visibility.Visible;

                // Setup auto-hide mouse idle timer
                SetupAutoHideTimer(imageView);
            });

            // Subscribe to clear event to cleanup
            imageView.ClearImageEventHandler += OnImageCleared;
        }

        private void SetupAutoHideTimer(ImageView imageView)
        {
            _lastMouseMoveTime = DateTime.Now;

            _mouseIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _mouseIdleTimer.Tick += (s, e) =>
            {
                if (!_autoHideEnabled || !_isPlaying) return;

                double idleSeconds = (DateTime.Now - _lastMouseMoveTime).TotalSeconds;
                if (idleSeconds > MouseIdleTimeoutSeconds)
                {
                    if (_videoToolBar != null && _videoToolBar.Opacity > 0)
                        _videoToolBar.Opacity = 0;
                }
            };
            _mouseIdleTimer.Start();

            imageView.MouseMove += OnImageViewMouseMove;
            imageView.MouseEnter += OnImageViewMouseMove;
        }

        private void OnImageViewMouseMove(object sender, MouseEventArgs e)
        {
            _lastMouseMoveTime = DateTime.Now;
            if (_videoToolBar != null && _videoToolBar.Opacity < 0.8)
                _videoToolBar.Opacity = 0.8;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Click-to-seek: when user clicks (not drags) the slider, seek to that position
            if (!_isDragging && _videoHandle > 0 && _progressSlider != null)
            {
                int targetFrame = (int)_progressSlider.Value;
                OpenCVMediaHelper.M_VideoSeek(_videoHandle, targetFrame);
                SyncAudioSeek(targetFrame);
            }
        }

        private void InitAudioPlayer()
        {
            if (string.IsNullOrEmpty(_currentFilePath)) return;

            try
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.Open(new Uri(_currentFilePath, UriKind.Absolute));
                _mediaPlayer.SpeedRatio = _currentSpeed;
                _mediaPlayer.IsMuted = _isMuted;
                // Pause immediately — audio starts only when user clicks Play
                _mediaPlayer.Pause();
            }
            catch (Exception ex)
            {
                log.Warn("Could not initialize audio player", ex);
                _mediaPlayer = null;
            }
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            _isMuted = !_isMuted;
            if (_mediaPlayer != null)
            {
                _mediaPlayer.IsMuted = _isMuted;
            }
            if (_muteButton != null)
            {
                _muteButton.Content = _isMuted ? "🔇" : "🔊";
            }
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

            // Stop and reset audio
            SyncAudioSeek(0);
        }

        private void PlayVideo()
        {
            if (_videoHandle <= 0 || _isPlaying) return;

            _isPlaying = true;
            _droppedFrameCount = 0;
            _lastSyncFrame = 0;
            UpdatePlayPauseButton(true);

            _frameCallbackDelegate = OnFrameReceived;
            _statusCallbackDelegate = OnStatusChanged;

            OpenCVMediaHelper.M_VideoPlay(_videoHandle, _frameCallbackDelegate, _statusCallbackDelegate, IntPtr.Zero);

            // Start audio playback in sync
            SyncAudioPlay();
        }

        private void PauseVideo()
        {
            if (_videoHandle <= 0 || !_isPlaying) return;

            OpenCVMediaHelper.M_VideoPause(_videoHandle);
            _isPlaying = false;
            UpdatePlayPauseButton(false);

            // Show toolbar when paused
            if (_videoToolBar != null)
                _videoToolBar.Opacity = 0.8;

            // Pause audio
            SyncAudioPause();
        }

        private void OnFrameReceived(int handle, ref HImage frame, int currentFrame, int totalFrames, IntPtr userData)
        {
            // Frame dropping: if previous frame is still being processed by UI, skip this frame
            if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) != 0)
            {
                Interlocked.Increment(ref _droppedFrameCount);
                return;
            }

            try
            {
                HImage localFrame = frame;
                int localCurrentFrame = currentFrame;
                // Invoke: 同步等待UI线程完成渲染
                // 消费者被阻塞没关系 — 生产者不再等待消费者，时间稳定
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (_videoHandle <= 0) return;

                        UpdateFrameDisplay(localFrame);

                        // Throttle slider/time updates to reduce UI overhead
                        int count = Interlocked.Increment(ref _uiUpdateCounter);
                        if (count % 10 == 0)
                        {
                            if (!_isDragging)
                            {
                                UpdateSliderPosition(localCurrentFrame);
                            }

                            UpdateTimeDisplay(localCurrentFrame);
                            UpdateFrameInfoDisplay(localCurrentFrame, localFrame);

                            // Audio-video sync correction when frames are dropped
                            CorrectAudioSync(localCurrentFrame);
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isProcessingFrame, 0);
                    }
                });
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _isProcessingFrame, 0);
                log.Error("Error in video frame callback", ex);
            }
        }

        private void CorrectAudioSync(int currentFrame)
        {
            var player = _mediaPlayer;
            if (player == null || !_isPlaying) return;

            try
            {
                double fps = _videoInfo.fps > 0 ? _videoInfo.fps : 30.0;
                double videoTimeSeconds = currentFrame / fps;
                double audioTimeSeconds = player.Position.TotalSeconds;
                double drift = Math.Abs(videoTimeSeconds - audioTimeSeconds);

                // If drift exceeds threshold, re-sync audio position
                if (drift > AudioSyncDriftThresholdSeconds)
                {
                    var position = TimeSpan.FromSeconds(videoTimeSeconds);
                    player.Position = position;
                }
            }
            catch (Exception ex)
            {
                log.Warn("Audio sync correction failed", ex);
            }
        }

        private void UpdateFrameDisplay(HImage frame)
        {
            if (_writeableBitmap != null &&
                _writeableBitmap.PixelWidth == frame.cols &&
                _writeableBitmap.PixelHeight == frame.rows)
            {
                // Fast path: reuse existing WriteableBitmap with parallel copy for large frames
                UpdateWriteableBitmapFast(_writeableBitmap, frame);
            }
            else
            {
                _writeableBitmap = frame.ToWriteableBitmap();
                if (_imageView?.ImageShow != null)
                    _imageView.ImageShow.Source = _writeableBitmap;
            }
        }

        /// <summary>
        /// High-performance WriteableBitmap update using parallel memory copy for large frames.
        /// For 8K (7680×4320×3ch) frames, parallel copy reduces time from ~25ms to ~5ms.
        /// </summary>
        private static void UpdateWriteableBitmapFast(WriteableBitmap writeableBitmap, HImage hImage)
        {
            writeableBitmap.Lock();
            try
            {
                int bytesPerRow = hImage.cols * hImage.channels * (hImage.depth / 8);
                int rows = hImage.rows;
                long totalBytes = (long)rows * bytesPerRow;

                unsafe
                {
                    byte* pSrc = (byte*)hImage.pData;
                    byte* pDst = (byte*)writeableBitmap.BackBuffer;
                    int srcStride = hImage.stride;
                    int dstStride = writeableBitmap.BackBufferStride;

                    if (totalBytes > 1024 * 1024) // > 1MB: use parallel copy
                    {
                        Parallel.For(0, rows, new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
                        }, y =>
                        {
                            byte* src = pSrc + ((long)y * srcStride);
                            byte* dst = pDst + ((long)y * dstStride);
                            Buffer.MemoryCopy(src, dst, bytesPerRow, bytesPerRow);
                        });
                    }
                    else
                    {
                        for (int y = 0; y < rows; y++)
                        {
                            Buffer.MemoryCopy(pSrc, pDst, bytesPerRow, bytesPerRow);
                            pSrc += srcStride;
                            pDst += dstStride;
                        }
                    }
                }

                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, hImage.cols, hImage.rows));
            }
            finally
            {
                writeableBitmap.Unlock();
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
                        SyncAudioPause();
                        break;
                    case 1: // Playing
                        _isPlaying = true;
                        UpdatePlayPauseButton(true);
                        break;
                    case 2: // Ended
                        _isPlaying = false;
                        UpdatePlayPauseButton(false);
                        SyncAudioPause();
                        // Reset to beginning
                        if (_videoHandle > 0)
                        {
                            OpenCVMediaHelper.M_VideoSeek(_videoHandle, 0);
                            UpdateSliderPosition(0);
                            SyncAudioSeek(0);
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

        private void UpdateFrameInfoDisplay(int currentFrame, HImage frame)
        {
            if (_frameInfoTextBlock == null) return;
            // Always show original source dimensions for consistency
            string info = $"{currentFrame}/{_videoInfo.totalFrames} [{_videoInfo.width}x{_videoInfo.height}]";
            if (_currentResizeScale < 1.0)
                info += $" @{_currentResizeScale:0.###}x";
            if (_droppedFrameCount > 0)
                info += $" D:{_droppedFrameCount}";
            if (_frameInfoTextBlock.Dispatcher.CheckAccess())
            {
                _frameInfoTextBlock.Text = info;
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _frameInfoTextBlock.Text = info;
                });
            }
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            if (_videoHandle <= 0 || _progressSlider == null) return;
            int targetFrame = (int)_progressSlider.Value;
            OpenCVMediaHelper.M_VideoSeek(_videoHandle, targetFrame);
            SyncAudioSeek(targetFrame);
        }

        private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_videoHandle <= 0 || _speedComboBox == null) return;

            if (_speedComboBox.SelectedItem is ComboBoxItem item && item.Tag is double speed)
            {
                _currentSpeed = speed;
                OpenCVMediaHelper.M_VideoSetPlaybackSpeed(_videoHandle, speed);
                SyncAudioSpeed(speed);
            }
        }

        private void ResizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_videoHandle <= 0 || _resizeComboBox == null) return;

            if (_resizeComboBox.SelectedItem is ComboBoxItem item && item.Tag is double scale)
            {
                _currentResizeScale = scale;
                OpenCVMediaHelper.M_VideoSetResizeScale(_videoHandle, scale);
                // Force WriteableBitmap re-creation on next frame
                _writeableBitmap = null;
            }
        }

        // --- Audio sync helpers ---

        private void SyncAudioPlay()
        {
            var player = _mediaPlayer;
            if (player == null) return;
            try
            {
                if (player.Dispatcher.CheckAccess())
                {
                    player.Play();
                }
                else
                {
                    Application.Current?.Dispatcher.Invoke(() => player.Play());
                }
            }
            catch (Exception ex) { log.Warn("Audio play failed", ex); }
        }

        private void SyncAudioPause()
        {
            var player = _mediaPlayer;
            if (player == null) return;
            try
            {
                if (player.Dispatcher.CheckAccess())
                {
                    player.Pause();
                }
                else
                {
                    Application.Current?.Dispatcher.Invoke(() => player.Pause());
                }
            }
            catch (Exception ex) { log.Warn("Audio pause failed", ex); }
        }

        private void SyncAudioSeek(int frameIndex)
        {
            var player = _mediaPlayer;
            if (player == null) return;
            try
            {
                double fps = _videoInfo.fps > 0 ? _videoInfo.fps : 30.0;
                var position = TimeSpan.FromSeconds(frameIndex / fps);
                if (player.Dispatcher.CheckAccess())
                {
                    player.Position = position;
                }
                else
                {
                    Application.Current?.Dispatcher.Invoke(() => player.Position = position);
                }
            }
            catch (Exception ex) { log.Warn("Audio seek failed", ex); }
        }

        private void SyncAudioSpeed(double speed)
        {
            var player = _mediaPlayer;
            if (player == null) return;
            try
            {
                if (player.Dispatcher.CheckAccess())
                {
                    player.SpeedRatio = speed;
                }
                else
                {
                    Application.Current?.Dispatcher.Invoke(() => player.SpeedRatio = speed);
                }
            }
            catch (Exception ex) { log.Warn("Audio speed change failed", ex); }
        }

        private void CloseVideo()
        {
            // Stop auto-hide timer
            if (_mouseIdleTimer != null)
            {
                _mouseIdleTimer.Stop();
                _mouseIdleTimer = null;
            }

            // Remove mouse event handlers
            if (_imageView != null)
            {
                _imageView.MouseMove -= OnImageViewMouseMove;
                _imageView.MouseEnter -= OnImageViewMouseMove;
            }

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

            // Close audio player
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Close();
                    _mediaPlayer = null;
                }
            });

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
                    if (_muteButton != null && _videoToolBar.Items.Contains(_muteButton))
                        _videoToolBar.Items.Remove(_muteButton);
                    if (_progressSlider != null && _videoToolBar.Items.Contains(_progressSlider))
                        _videoToolBar.Items.Remove(_progressSlider);
                    if (_timeTextBlock != null && _videoToolBar.Items.Contains(_timeTextBlock))
                        _videoToolBar.Items.Remove(_timeTextBlock);
                    if (_frameInfoTextBlock != null && _videoToolBar.Items.Contains(_frameInfoTextBlock))
                        _videoToolBar.Items.Remove(_frameInfoTextBlock);
                    if (_speedComboBox != null && _videoToolBar.Items.Contains(_speedComboBox))
                        _videoToolBar.Items.Remove(_speedComboBox);
                    if (_resizeComboBox != null && _videoToolBar.Items.Contains(_resizeComboBox))
                        _videoToolBar.Items.Remove(_resizeComboBox);
                    if (_autoHideCheckBox != null && _videoToolBar.Items.Contains(_autoHideCheckBox))
                        _videoToolBar.Items.Remove(_autoHideCheckBox);

                    // Restore toolbar opacity
                    _videoToolBar.Opacity = 0.8;
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
            _currentFilePath = null;
        }
    }
}
