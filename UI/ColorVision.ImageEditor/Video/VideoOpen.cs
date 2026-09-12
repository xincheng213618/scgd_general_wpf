using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Documents;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Video
{
    [FileExtension(".mp4|.avi|.mkv|.mov|.wmv|.flv|.webm")]
    public record class VideoOpen(EditorContext EditorContext) : IImageOpen, IImageOpenEditorToolLifecycle, IDisposable
    {
        private VideoPlaybackSession? _session;
        private WriteableBitmap? _writeableBitmap;
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
        private bool _mutePreference;
        private Guid _streamSessionId;
        private DispatcherTimer? _mouseIdleTimer;
        private bool _autoHideEnabled = true;
        private CheckBox? _autoHideCheckBox;
        private DateTime _lastMouseMoveTime = DateTime.Now;
        private const double MouseIdleTimeoutSeconds = 3.0;
        private OpenCVMediaHelper.VideoInfo VideoInfo => _session?.Info ?? default;

        public void OpenImage(EditorContext context, string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            Close();
            FileInfo fileInfo = new(filePath);
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, filePath, nameof(VideoOpen), "打开器接收到的视频源路径");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, fileInfo.Name, nameof(VideoOpen), "当前视频文件名");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, fileInfo.Length, nameof(VideoOpen), "当前视频文件大小（字节）");

            _session = new VideoPlaybackSession(context.ProcessingContext.Dispatcher, frame => PublishFrame(context.ProcessingContext, frame));
            _session.SetMuted(_mutePreference);
            _session.StateChanged += OnPlaybackStateChanged;
            try
            {
                if (!_session.Open(filePath))
                {
                    Close();
                    MessageBox.Show($"Failed to open video file: {filePath}");
                    return;
                }
                OpenCVMediaHelper.VideoInfo info = _session.Info;
                context.Config.SetImageMetadata(ImageViewPropertyKeys.VideoWidth, info.width, nameof(VideoOpen), "视频帧宽度");
                context.Config.SetImageMetadata(ImageViewPropertyKeys.VideoHeight, info.height, nameof(VideoOpen), "视频帧高度");
                context.Config.SetImageMetadata(ImageViewPropertyKeys.VideoFPS, info.fps, nameof(VideoOpen), "视频帧率");
                context.Config.SetImageMetadata(ImageViewPropertyKeys.VideoTotalFrames, info.totalFrames, nameof(VideoOpen), "视频总帧数");
                double fps = info.fps > 0 ? info.fps : 30.0;
                context.Config.SetImageMetadata(ImageViewPropertyKeys.VideoDuration, TimeSpan.FromSeconds(info.totalFrames / fps).ToString(@"hh\:mm\:ss"), nameof(VideoOpen), "视频总时长");
                SetupVideoControls(context);
                RefreshPlaybackControls();
            }
            catch
            {
                Close();
                throw;
            }
        }

        private void SetupVideoControls(EditorContext context)
        {
            var imageView = context.ImageView;
            _videoToolBar = imageView.ToolBarAl;

            context.ProcessingContext.Dispatcher.Invoke(() =>
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

                // Progress slider - supports both drag and click-to-seek
                _progressSlider = new Slider
                {
                    Minimum = 0,
                    Maximum = VideoInfo.totalFrames > 0 ? VideoInfo.totalFrames - 1 : 0,
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

                double displayFps = VideoInfo.fps > 0 ? VideoInfo.fps : 30.0;
                // Time display
                _timeTextBlock = new TextBlock
                {
                    Text = "00:00:00 / " + TimeSpan.FromSeconds(VideoInfo.totalFrames / displayFps).ToString(@"hh\:mm\:ss"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0),
                    FontSize = 11
                };

                // Frame info display (shows original source dimensions)
                _frameInfoTextBlock = new TextBlock
                {
                    Text = $"0/{VideoInfo.totalFrames} [{VideoInfo.width}x{VideoInfo.height}]",
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

            // Closing through the config event also covers opening another file.
            context.Config.Cleared -= OnConfigCleared;
            context.Config.Cleared += OnConfigCleared;
            context.ProcessingContext.StreamPresentation.FramePresented -= OnFramePresented;
            context.ProcessingContext.StreamPresentation.FramePresented += OnFramePresented;
        }

        private void SetupAutoHideTimer(FrameworkElement imageView)
        {
            _lastMouseMoveTime = DateTime.Now;

            _mouseIdleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _mouseIdleTimer.Tick += (s, e) =>
            {
                if (!_autoHideEnabled || _session?.IsPlaying != true) return;

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
            if (!_isDragging && _progressSlider != null) Seek((int)_progressSlider.Value);
        }

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session != null) _session.SetMuted(!_session.IsMuted);
        }

        private void OnConfigCleared(object? sender, EventArgs e) => Close();

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session?.IsPlaying == true) _session.Pause();
            else _session?.Play();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            EditorContext.ProcessingContext.StreamPresentation.ResetSource(_streamSessionId);
            _session?.Stop();
        }

        private void Seek(int frameIndex)
        {
            EditorContext.ProcessingContext.StreamPresentation.ResetSource(_streamSessionId);
            _session?.Seek(frameIndex);
        }

        private void PublishFrame(ImageProcessingContext context, VideoFrameDelivery frame)
        {
            _streamSessionId = frame.SessionId;
            HImage image = frame.Image;
            if (_writeableBitmap != null && _writeableBitmap.PixelWidth == image.cols && _writeableBitmap.PixelHeight == image.rows)
            {
                UpdateWriteableBitmapFast(_writeableBitmap, image);
            }
            else
            {
                _writeableBitmap = image.ToWriteableBitmap();
            }
            if (frame.IsInitialFrame)
            {
                WriteableBitmap source = _writeableBitmap.Clone();
                source.Freeze();
                context.SetImageSource(source);
                context.UpdateZoomAndScale();
            }
            else
            {
                context.StreamPresentation.Submit(_writeableBitmap, frame.SessionId);
            }
        }

        private static void UpdateWriteableBitmapFast(WriteableBitmap writeableBitmap, HImage hImage)
        {
            writeableBitmap.Lock();
            try
            {
                long rowBytes = (long)hImage.cols * hImage.channels * (hImage.depth / 8);
                int rows = hImage.rows;
                int dstStride = writeableBitmap.BackBufferStride;
                if (hImage.pData == IntPtr.Zero ||
                    hImage.rows <= 0 ||
                    hImage.cols <= 0 ||
                    hImage.channels <= 0 ||
                    hImage.depth <= 0 ||
                    hImage.depth % 8 != 0 ||
                    rowBytes <= 0 ||
                    rowBytes > int.MaxValue)
                {
                    throw new ArgumentException("Invalid HImage layout.");
                }

                int bytesPerRow = (int)rowBytes;
                int srcStride = hImage.stride > 0 ? hImage.stride : bytesPerRow;
                if (srcStride < bytesPerRow || dstStride < bytesPerRow)
                {
                    throw new ArgumentException("Invalid HImage stride.");
                }

                long totalBytes = (long)rows * bytesPerRow;

                unsafe
                {
                    byte* pSrc = (byte*)hImage.pData;
                    byte* pDst = (byte*)writeableBitmap.BackBuffer;

                    if (srcStride == bytesPerRow && dstStride == bytesPerRow)
                    {
                        Buffer.MemoryCopy(pSrc, pDst, totalBytes, totalBytes);
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

        private void OnPlaybackStateChanged(object? sender, EventArgs e)
        {
            if (_session != null && _streamSessionId != _session.SessionId)
            {
                EditorContext.ProcessingContext.StreamPresentation.ResetSource(_streamSessionId);
                _streamSessionId = _session.SessionId;
            }
            RefreshPlaybackControls();
        }

        private void OnFramePresented(Guid sourceId, WriteableBitmap source)
        {
            if (sourceId == _streamSessionId && ReferenceEquals(EditorContext.ProcessingContext.ViewBitmapSource, source))
                ImageSourceMetadata.TryApply(source, EditorContext.Config);
        }

        private void RefreshPlaybackControls()
        {
            VideoPlaybackSession? session = _session;
            if (session == null) return;
            if (_playPauseButton != null) _playPauseButton.Content = session.IsPlaying ? "⏸" : "▶";
            if (_muteButton != null) _muteButton.Content = session.IsMuted ? "🔇" : "🔊";
            if (!session.IsPlaying && _videoToolBar != null) _videoToolBar.Opacity = 0.8;
            if (!_isDragging && _progressSlider != null) _progressSlider.Value = session.CurrentFrame;
            OpenCVMediaHelper.VideoInfo info = session.Info;
            double fps = info.fps > 0 ? info.fps : 30.0;
            TimeSpan current = TimeSpan.FromSeconds(session.CurrentFrame / fps);
            TimeSpan total = TimeSpan.FromSeconds(info.totalFrames / fps);
            if (_timeTextBlock != null) _timeTextBlock.Text = $"{current:hh\\:mm\\:ss} / {total:hh\\:mm\\:ss}";
            if (_frameInfoTextBlock != null)
            {
                string text = $"{session.CurrentFrame}/{info.totalFrames} [{info.width}x{info.height}]";
                if (session.ResizeScale < 1.0) text += $" @{session.ResizeScale:0.###}x";
                if (session.DroppedFrameCount > 0) text += $" D:{session.DroppedFrameCount}";
                _frameInfoTextBlock.Text = text;
            }
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e) => _isDragging = true;

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            if (_progressSlider != null) Seek((int)_progressSlider.Value);
        }

        private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_speedComboBox?.SelectedItem is ComboBoxItem { Tag: double speed }) _session?.SetPlaybackSpeed(speed);
        }

        private void ResizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_resizeComboBox?.SelectedItem is ComboBoxItem { Tag: double scale })
            {
                _session?.SetResizeScale(scale);
                _writeableBitmap = null;
            }
        }

        public void OnEditorToolsActivated(EditorContext context) { }
        public void OnEditorToolsDeactivated(EditorContext context) => Close();

        public void Close()
        {
            Dispatcher dispatcher = EditorContext.ProcessingContext.Dispatcher;
            if (!dispatcher.CheckAccess())
            {
                dispatcher.Invoke(Close);
                return;
            }

            VideoPlaybackSession? session = _session;
            _session = null;
            if (session != null)
            {
                EditorContext.ProcessingContext.StreamPresentation.ResetSource(_streamSessionId);
                _mutePreference = session.IsMuted;
                session.StateChanged -= OnPlaybackStateChanged;
            }
            try
            {
                session?.Dispose();
            }
            finally
            {
                _mouseIdleTimer?.Stop();
                _mouseIdleTimer = null;
                EditorContext.Config.Cleared -= OnConfigCleared;
                EditorContext.ProcessingContext.StreamPresentation.FramePresented -= OnFramePresented;
                EditorContext.ImageView.MouseMove -= OnImageViewMouseMove;
                EditorContext.ImageView.MouseEnter -= OnImageViewMouseMove;
                if (_videoToolBar != null)
                {
                    // Other workflow components may share this toolbar.
                    if (_playPauseButton != null) _videoToolBar.Items.Remove(_playPauseButton);
                    if (_stopButton != null) _videoToolBar.Items.Remove(_stopButton);
                    if (_muteButton != null) _videoToolBar.Items.Remove(_muteButton);
                    if (_progressSlider != null) _videoToolBar.Items.Remove(_progressSlider);
                    if (_timeTextBlock != null) _videoToolBar.Items.Remove(_timeTextBlock);
                    if (_frameInfoTextBlock != null) _videoToolBar.Items.Remove(_frameInfoTextBlock);
                    if (_speedComboBox != null) _videoToolBar.Items.Remove(_speedComboBox);
                    if (_resizeComboBox != null) _videoToolBar.Items.Remove(_resizeComboBox);
                    if (_autoHideCheckBox != null) _videoToolBar.Items.Remove(_autoHideCheckBox);
                    _videoToolBar.Opacity = 0.8;
                }
                _writeableBitmap = null;
                _streamSessionId = Guid.Empty;
                _progressSlider = null;
                _playPauseButton = null;
                _stopButton = null;
                _muteButton = null;
                _speedComboBox = null;
                _resizeComboBox = null;
                _timeTextBlock = null;
                _frameInfoTextBlock = null;
                _videoToolBar = null;
                _autoHideCheckBox = null;
                _isDragging = false;
            }
        }

        public void Dispose() => Close();
    }
}
