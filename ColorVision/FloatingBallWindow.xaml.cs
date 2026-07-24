#pragma warning disable CA1001,CA1822,CA1863,CS8602
using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ColorVision.FloatingBall
{
    /// <summary>
    /// FloatingBallWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FloatingBallWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FloatingBallWindow));
        private readonly Queue<DesktopPetNotification> _notifications = new();
        private readonly DispatcherTimer _motionTimer = new();
        private readonly DispatcherTimer _messageTimer = new();
        private readonly DispatcherTimer _idleTipTimer = new();
        private readonly DispatcherTimer _transientStateTimer = new();
        private readonly Random _random = new();
        private readonly DesktopPetSpriteAnimator _spriteAnimator;
        private DateTime _motionStartedAt = DateTime.Now;
        private DesktopPetActivityState _activityState = DesktopPetActivityState.Idle;
        private DesktopPetActivityState _visualActivityState = DesktopPetActivityState.Idle;
        private bool _isDragging;
        private bool _dragHasMoved;
        private double _lastDragLeft;
        private double _lastDragTop;
        private bool _isShowingMessage;
        private bool _isClosingFromConfig;
        private int _assetLoadVersion;
        private ConfirmableAction? _currentConfirmationAction;
        private CancellationTokenSource? _confirmationOperationCts;

        public static FloatingBallWindowConfig WindowConfig => ConfigService.Instance.GetRequiredService<FloatingBallWindowConfig>();
        public static DesktopPetConfig PetConfig => DesktopPetConfig.Instance;

        public FloatingBallWindow()
        {
            InitializeComponent();
            _spriteAnimator = new DesktopPetSpriteAnimator(frame => SpritePetImage.Source = frame);
            DataContext = PetConfig;
            PlaceDefaultPositionIfNeeded();
            WindowConfig.SetWindow(this);
            ConfigureTimers();
            PetConfig.PropertyChanged += PetConfig_PropertyChanged;
            ApplyActivityVisual(_activityState);
        }

        public void CloseFromConfig()
        {
            _isClosingFromConfig = true;
            Close();
        }

        public void ReloadSelectedAsset()
        {
            _ = ReloadSelectedAssetAsync();
        }

        public void SetActivityState(DesktopPetActivityState state)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => SetActivityState(state));
                return;
            }

            _transientStateTimer.Stop();
            _activityState = state;
            _visualActivityState = state;
            ApplyActivityVisual(state);
            _spriteAnimator.SetState(state);
        }

        public void PlayTransientActivity(DesktopPetActivityState state, DesktopPetActivityState returnState)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => PlayTransientActivity(state, returnState));
                return;
            }

            _activityState = returnState;
            _visualActivityState = state;
            ApplyActivityVisual(state);
            _spriteAnimator.PlayTransient(state, returnState);
            _transientStateTimer.Stop();
            _transientStateTimer.Interval = TimeSpan.FromSeconds(4);
            _transientStateTimer.Start();
        }

        public void EnqueueNotification(DesktopPetNotification notification)
        {
            if (!PetConfig.ShowNotifications || string.IsNullOrWhiteSpace(notification.Message))
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => EnqueueNotification(notification));
                return;
            }

            _notifications.Enqueue(notification);
            TryShowNextNotification();
        }

        public void ShowCopilotConfirmation(ConfirmableAction? action, int totalPending)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => ShowCopilotConfirmation(action, totalPending));
                return;
            }

            if (action?.IsPending != true)
            {
                _currentConfirmationAction = null;
                CopilotApprovalPopup.IsOpen = false;
                SetConfirmationBusy(false);
                TryShowNextNotification();
                return;
            }

            _messageTimer.Stop();
            MessageBubble.Visibility = Visibility.Collapsed;
            MessageBubble.Opacity = 0;
            MessageBubbleTransform.Y = 8;
            _isShowingMessage = false;

            _currentConfirmationAction = action;
            ConfirmationTitle.Text = string.IsNullOrWhiteSpace(action.Title)
                ? "Copilot 操作等待确认"
                : action.Title;
            ConfirmationDescription.Text = string.IsNullOrWhiteSpace(action.Description)
                ? "请确认这项操作是否符合你的预期。"
                : action.Description;
            ConfirmationToolText.Text = string.IsNullOrWhiteSpace(action.ToolName)
                ? "工具 · 未知"
                : $"工具 · {action.ToolName}";
            ConfirmationExpiryText.Text = $"到期 · {action.ReviewDeadlineLabel}";
            ConfirmationCountText.Text = totalPending > 1
                ? $"需确认 · {totalPending}"
                : "需确认";
            SetConfirmationBusy(false);
            CopilotApprovalPopup.IsOpen = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            var originalLeft = Left;
            var originalTop = Top;
            _isDragging = true;
            _dragHasMoved = false;
            _lastDragLeft = Left;
            _lastDragTop = Top;
            try
            {
                DragMove();
                WindowConfig.SetConfig(this);
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                _isDragging = false;
                if (_dragHasMoved)
                {
                    _visualActivityState = _activityState;
                    ApplyActivityVisual(_activityState);
                    _spriteAnimator.SetState(_activityState);
                }
            }

            var wasClick = !_dragHasMoved
                && Math.Abs(Left - originalLeft) < 2
                && Math.Abs(Top - originalTop) < 2;
            if (!wasClick)
                return;

            TapPet();
            if (e.ClickCount >= 2)
            {
                DesktopPetService.GetInstance().OpenCopilot();
                e.Handled = true;
                return;
            }

            if (wasClick && _activityState == DesktopPetActivityState.Waiting)
                DesktopPetService.GetInstance().OpenCopilot();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            BuildContextMenu();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _motionStartedAt = DateTime.Now;
            _motionTimer.Start();
            ResetIdleTipTimer();
            await ReloadSelectedAssetAsync();
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            if (!_isDragging)
                return;

            var deltaX = Left - _lastDragLeft;
            var deltaY = Top - _lastDragTop;
            if (Math.Abs(deltaX) < 4 && Math.Abs(deltaY) < 4)
                return;

            _dragHasMoved = true;
            _lastDragLeft = Left;
            _lastDragTop = Top;

            var dragState = DesktopPetAnimationPlan.ResolveDragState(_visualActivityState, deltaX);
            if (dragState == _visualActivityState)
                return;

            _visualActivityState = dragState;
            ApplyActivityVisual(dragState);
            _spriteAnimator.SetState(dragState);
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            Interlocked.Increment(ref _assetLoadVersion);
            _motionTimer.Stop();
            _messageTimer.Stop();
            _idleTipTimer.Stop();
            _transientStateTimer.Stop();
            _confirmationOperationCts?.Cancel();
            _confirmationOperationCts?.Dispose();
            _confirmationOperationCts = null;
            CopilotApprovalPopup.IsOpen = false;
            _spriteAnimator.Dispose();
            PetConfig.PropertyChanged -= PetConfig_PropertyChanged;
            DesktopPetService.GetInstance().Detach(this);

            if (!_isClosingFromConfig && MainWindowConfig.Instance.OpenFloatingBall)
                MainWindowConfig.Instance.OpenFloatingBall = false;
        }

        private void PetConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DesktopPetConfig.SelectedPetId))
            {
                ReloadSelectedAsset();
            }
            else if (e.PropertyName == nameof(DesktopPetConfig.EnableIdleTips)
                     || e.PropertyName == nameof(DesktopPetConfig.IdleTipIntervalMinutes))
            {
                ResetIdleTipTimer();
            }
            else if (e.PropertyName == nameof(DesktopPetConfig.PetScale)
                     || e.PropertyName == nameof(DesktopPetConfig.PetOpacity))
            {
                ClampVisualConfig();
            }
            else if (e.PropertyName == nameof(DesktopPetConfig.EnableCopilotIntegration)
                     || e.PropertyName == nameof(DesktopPetConfig.ShowCopilotNotifications)
                     || e.PropertyName == nameof(DesktopPetConfig.ShowNotifications))
            {
                DesktopPetService.GetInstance().RefreshCopilotIntegration();
            }
        }

        private void ConfigureTimers()
        {
            _motionTimer.Interval = TimeSpan.FromMilliseconds(33);
            _motionTimer.Tick += (_, _) =>
            {
                var seconds = (DateTime.Now - _motionStartedAt).TotalSeconds;
                var isRunning = _visualActivityState is DesktopPetActivityState.Running
                    or DesktopPetActivityState.RunningLeft
                    or DesktopPetActivityState.RunningRight;
                var speed = _visualActivityState switch
                {
                    DesktopPetActivityState.Running
                        or DesktopPetActivityState.RunningLeft
                        or DesktopPetActivityState.RunningRight => 3.1,
                    DesktopPetActivityState.Waiting => 1.45,
                    _ => 2.2,
                };
                var amplitude = isRunning ? 7 : 5;
                PetFloatTransform.Y = Math.Sin(seconds * speed) * amplitude;
                PetTiltTransform.Angle = Math.Sin(seconds * (speed * 0.42)) * (isRunning ? 2.1 : 1.4);
                ActivityGlow.Opacity = GetGlowBaseOpacity(_visualActivityState) + Math.Sin(seconds * speed) * 0.035;
            };

            _messageTimer.Tick += (_, _) =>
            {
                _messageTimer.Stop();
                HideCurrentNotification();
            };

            _idleTipTimer.Tick += (_, _) =>
            {
                if (PetConfig.EnableIdleTips && !_isShowingMessage)
                {
                    var tips = new[]
                    {
                        Properties.Resources.DesktopPetIdleTip1,
                        Properties.Resources.DesktopPetIdleTip2,
                        Properties.Resources.DesktopPetIdleTip3,
                        Properties.Resources.DesktopPetIdleTip4
                    };
                    EnqueueNotification(new DesktopPetNotification
                    {
                        Title = PetConfig.PetName,
                        Message = tips[_random.Next(tips.Length)],
                        Kind = DesktopPetNotificationKind.Info
                    });
                }
                ResetIdleTipTimer();
            };

            _transientStateTimer.Tick += (_, _) =>
            {
                _transientStateTimer.Stop();
                _visualActivityState = _activityState;
                ApplyActivityVisual(_activityState);
            };
        }

        private void PlaceDefaultPositionIfNeeded()
        {
            if (WindowConfig.Top != 0 || WindowConfig.Left != 0)
                return;

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var workingArea = screen.WorkingArea;

            double dpiX;
            double dpiY;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformFromDevice.M11;
                dpiY = source.CompositionTarget.TransformFromDevice.M22;
            }
            else
            {
                using var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
                dpiX = 96.0 / graphics.DpiX;
                dpiY = 96.0 / graphics.DpiY;
            }

            WindowConfig.Left = workingArea.Right * dpiX - Width - 80;
            WindowConfig.Top = workingArea.Bottom * dpiY - Height - 70;
        }

        private void ClampVisualConfig()
        {
            if (PetConfig.PetScale < 0.65)
                PetConfig.PetScale = 0.65;
            else if (PetConfig.PetScale > 1.45)
                PetConfig.PetScale = 1.45;

            if (PetConfig.PetOpacity < 0.35)
                PetConfig.PetOpacity = 0.35;
            else if (PetConfig.PetOpacity > 1)
                PetConfig.PetOpacity = 1;

        }

        private void BuildContextMenu()
        {
            var contextMenu = new ContextMenu();

            var openCopilot = new MenuItem { Header = "打开 Copilot" };
            openCopilot.Click += (_, _) => DesktopPetService.GetInstance().OpenCopilot();
            contextMenu.Items.Add(openCopilot);

            var testMessage = new MenuItem { Header = Properties.Resources.DesktopPetSendTestReminder };
            testMessage.Click += (_, _) => DesktopPetService.GetInstance().Notify(
                Properties.Resources.DesktopPetReminder,
                Properties.Resources.DesktopPetTestMessage,
                DesktopPetNotificationKind.Success);
            contextMenu.Items.Add(testMessage);

            var showMainWindow = new MenuItem { Header = Properties.Resources.DesktopPetShowMainWindow };
            showMainWindow.Click += (_, _) => DesktopPetService.GetInstance().ShowMainWindow();
            contextMenu.Items.Add(showMainWindow);

            var settings = new MenuItem { Header = "选择宠物与设置" };
            settings.Click += (_, _) => DesktopPetService.GetInstance().OpenSettings();
            contextMenu.Items.Add(settings);

            contextMenu.Items.Add(new Separator());

            var topmost = new MenuItem
            {
                Header = Properties.Resources.DesktopPetAlwaysOnTop,
                IsCheckable = true,
                IsChecked = PetConfig.AlwaysOnTop
            };
            topmost.Click += (_, _) => PetConfig.AlwaysOnTop = topmost.IsChecked;
            contextMenu.Items.Add(topmost);

            var notifications = new MenuItem
            {
                Header = Properties.Resources.DesktopPetShowNotifications,
                IsCheckable = true,
                IsChecked = PetConfig.ShowNotifications
            };
            notifications.Click += (_, _) => PetConfig.ShowNotifications = notifications.IsChecked;
            contextMenu.Items.Add(notifications);

            contextMenu.Items.Add(new Separator());

            var hide = new MenuItem { Header = Properties.Resources.DesktopPetHide };
            hide.Click += (_, _) => MainWindowConfig.Instance.OpenFloatingBall = false;
            contextMenu.Items.Add(hide);

            var exit = new MenuItem { Header = Properties.Resources.DesktopPetExit };
            exit.Click += (_, _) => Application.Current.Shutdown();
            contextMenu.Items.Add(exit);

            ContextMenu = contextMenu;
        }

        private void TapPet()
        {
            var scaleUp = new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(90)) { AutoReverse = true };
            PetTapScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleUp);
            PetTapScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleUp);
            _spriteAnimator.PlayTransient(DesktopPetActivityState.Jumping, _activityState, loopCount: 1);

            if (!_isShowingMessage && _random.NextDouble() < 0.35)
            {
                EnqueueNotification(new DesktopPetNotification
                {
                    Title = PetConfig.PetName,
                    Message = Properties.Resources.DesktopPetIdleTip1,
                    Kind = DesktopPetNotificationKind.Info
                });
            }
        }

        private void TryShowNextNotification()
        {
            if (_currentConfirmationAction != null || _isShowingMessage || _notifications.Count == 0)
                return;

            var notification = _notifications.Dequeue();
            _isShowingMessage = true;
            MessageTitle.Text = string.IsNullOrWhiteSpace(notification.Title) ? PetConfig.PetName : notification.Title;
            MessageText.Text = notification.Message;
            ApplyMessageColor(notification.Kind);

            MessageBubble.Visibility = Visibility.Visible;
            MessageBubble.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
            MessageBubbleTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)));

            var seconds = Math.Max(2, Math.Min(20, PetConfig.MessageDisplaySeconds));
            _messageTimer.Interval = TimeSpan.FromSeconds(seconds);
            _messageTimer.Start();
        }

        private void HideCurrentNotification()
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fade.Completed += (_, _) =>
            {
                MessageBubble.Visibility = Visibility.Collapsed;
                MessageBubbleTransform.Y = 8;
                _isShowingMessage = false;
                TryShowNextNotification();
            };

            MessageBubble.BeginAnimation(OpacityProperty, fade);
            MessageBubbleTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, TimeSpan.FromMilliseconds(160)));
        }

        private void ApplyMessageColor(DesktopPetNotificationKind kind)
        {
            var color = kind switch
            {
                DesktopPetNotificationKind.Success => "#3B1BA784",
                DesktopPetNotificationKind.Warning => "#4BD99000",
                DesktopPetNotificationKind.Error => "#4BDA3B3B",
                _ => "#3B2F5BFF"
            };
            MessageBubbleBorder.BorderBrush = BrushFromHex(color);
        }

        private void OpenConfirmationButton_Click(object sender, RoutedEventArgs e)
        {
            DesktopPetService.GetInstance().OpenCopilot();
        }

        private async void ApproveConfirmationButton_Click(object sender, RoutedEventArgs e)
        {
            var action = _currentConfirmationAction;
            if (action?.IsPending != true || _confirmationOperationCts != null)
                return;

            var result = MessageBox.Show(
                this,
                CopilotMcpConfirmationDecision.BuildApprovalPrompt(action),
                "Approve Copilot action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (result != MessageBoxResult.Yes)
                return;

            var cancellation = new CancellationTokenSource();
            _confirmationOperationCts = cancellation;
            SetConfirmationBusy(true, "正在处理…");
            try
            {
                var approvalResult = await DesktopPetService.GetInstance()
                    .ApproveCopilotActionAsync(action, cancellation.Token);
                DesktopPetService.GetInstance().Notify(
                    "Copilot",
                    BuildApprovalNotification(action, approvalResult),
                    approvalResult.Success
                        ? DesktopPetNotificationKind.Success
                        : DesktopPetNotificationKind.Error);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_confirmationOperationCts, cancellation))
                    _confirmationOperationCts = null;
                cancellation.Dispose();
                SetConfirmationBusy(false);
            }
        }

        private void RejectConfirmationButton_Click(object sender, RoutedEventArgs e)
        {
            var action = _currentConfirmationAction;
            if (action?.IsPending != true || _confirmationOperationCts != null)
                return;

            var rejected = DesktopPetService.GetInstance().RejectCopilotAction(action, out var message);
            DesktopPetService.GetInstance().Notify(
                "Copilot",
                rejected
                    ? $"已拒绝“{action.Title}”。"
                    : $"操作未能拒绝：{message}",
                rejected
                    ? DesktopPetNotificationKind.Info
                    : DesktopPetNotificationKind.Error);
        }

        private void SetConfirmationBusy(bool isBusy, string status = "")
        {
            ApproveConfirmationButton.IsEnabled = !isBusy;
            RejectConfirmationButton.IsEnabled = !isBusy;
            ConfirmationStatusText.Text = status;
            ConfirmationStatusText.Visibility = string.IsNullOrWhiteSpace(status)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private static string BuildApprovalNotification(
            ConfirmableAction action,
            CopilotConfirmationApprovalResult result)
        {
            if (!result.Success)
                return $"操作未能批准：{result.Message}";

            if (action.ResumesAgentOnApproval)
                return $"已批准“{action.Title}”，Copilot 将继续执行。";

            return result.ExecutedImmediately
                ? $"已批准并执行“{action.Title}”。"
                : $"已批准“{action.Title}”，等待请求方继续。";
        }

        private void ResetIdleTipTimer()
        {
            _idleTipTimer.Stop();
            if (!PetConfig.EnableIdleTips)
                return;

            var minutes = Math.Max(5, Math.Min(240, PetConfig.IdleTipIntervalMinutes));
            _idleTipTimer.Interval = TimeSpan.FromMinutes(minutes);
            _idleTipTimer.Start();
        }

        private async Task ReloadSelectedAssetAsync()
        {
            var loadVersion = Interlocked.Increment(ref _assetLoadVersion);
            try
            {
                var catalog = DesktopPetAssetCatalog.Shared;
                await catalog.EnsureLoadedAsync();
                var asset = catalog.GetSelectedOrDefault(PetConfig.SelectedPetId);
                if (loadVersion != Volatile.Read(ref _assetLoadVersion))
                    return;

                if (!asset.IsSpriteSheet)
                {
                    _spriteAnimator.SetSpriteSheet(null);
                    SpritePetImage.Source = null;
                    SpritePetImage.Visibility = Visibility.Collapsed;
                    DefaultPetImage.Visibility = Visibility.Visible;
                    return;
                }

                var spriteSheet = await Task.Run(() => DesktopPetSpriteSheet.Load(
                    asset.ReadSpriteSheetBytes(),
                    asset.SpriteVersionNumber));
                if (loadVersion != Volatile.Read(ref _assetLoadVersion))
                {
                    spriteSheet.Dispose();
                    return;
                }

                _spriteAnimator.SetSpriteSheet(spriteSheet);
                DefaultPetImage.Visibility = Visibility.Collapsed;
                SpritePetImage.Visibility = Visibility.Visible;
                _spriteAnimator.SetState(_activityState);
            }
            catch (Exception ex)
            {
                log.Warn("桌面宠物素材加载失败，已回退到默认素材", ex);
                _spriteAnimator.SetSpriteSheet(null);
                SpritePetImage.Source = null;
                SpritePetImage.Visibility = Visibility.Collapsed;
                DefaultPetImage.Visibility = Visibility.Visible;
            }
        }

        private void ApplyActivityVisual(DesktopPetActivityState state)
        {
            ActivityGlow.Fill = BrushFromHex(state switch
            {
                DesktopPetActivityState.Waiting => "#55F2A900",
                DesktopPetActivityState.Review => "#5534A853",
                DesktopPetActivityState.Waving => "#556C5CE7",
                DesktopPetActivityState.Failed => "#55DA3B3B",
                DesktopPetActivityState.Jumping => "#553C6DF0",
                _ => "#443C6DF0",
            });
            ActivityGlow.Opacity = GetGlowBaseOpacity(state);

            ActivityBadge.Visibility = state == DesktopPetActivityState.Idle
                ? Visibility.Collapsed
                : Visibility.Visible;
            ActivityBadge.Background = BrushFromHex(state switch
            {
                DesktopPetActivityState.Waiting => "#E6D99000",
                DesktopPetActivityState.Review => "#E61B8A42",
                DesktopPetActivityState.Waving => "#E66C5CE7",
                DesktopPetActivityState.Failed => "#E6C43131",
                _ => "#E62F5BFF",
            });
            ActivityBadgeText.Text = state switch
            {
                DesktopPetActivityState.Waiting => "!",
                DesktopPetActivityState.Review => "✓",
                DesktopPetActivityState.Waving => "Hi",
                DesktopPetActivityState.Failed => "×",
                DesktopPetActivityState.Jumping => "·",
                _ => "AI",
            };
        }

        private static double GetGlowBaseOpacity(DesktopPetActivityState state)
        {
            return state switch
            {
                DesktopPetActivityState.Idle => 0.12,
                DesktopPetActivityState.Running
                    or DesktopPetActivityState.RunningLeft
                    or DesktopPetActivityState.RunningRight => 0.27,
                DesktopPetActivityState.Waiting => 0.32,
                DesktopPetActivityState.Failed => 0.34,
                _ => 0.24,
            };
        }

        private static Brush BrushFromHex(string color)
        {
            var brush = (Brush)new BrushConverter().ConvertFromString(color);
            if (brush.CanFreeze)
                brush.Freeze();
            return brush;
        }

    }
}
