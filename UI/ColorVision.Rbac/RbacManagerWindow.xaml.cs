#pragma warning disable CA1822,CA1860,CS8625
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Rbac.Services;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Rbac
{
    public class MenuRbacManager : IRightMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var icon = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = "\uE77B",
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            return new[]
            {
                new MenuItemMetadata
                {
                    Order = 300,
                    Command = new RelayCommand(_ => new RbacManagerWindow
                    {
                        Owner = Application.Current.GetActiveWindow(),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    }.ShowDialog()),
                    Icon = icon,
                }
            };
        }
    }

    /// <summary>
    /// 用户资料、应用使用情况和流程执行概览。
    /// </summary>
    public partial class RbacManagerWindow : Window, INotifyPropertyChanged, IDisposable
    {
        private const int ActivityDayCount = 364;
        private readonly DispatcherTimer _runtimeTimer;
        private CancellationTokenSource? _statisticsCancellation;
        private PropertyChangedEventHandler? _configPropertyChangedHandler;
        private EventHandler? _mysqlConnectionChangedHandler;
        private bool _disposed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public RbacManager Manager { get; }
        public RbacManagerConfig Config => Manager.Config;
        public RelayCommand LoginCommand => Manager.LoginCommand;
        public RelayCommand EditCommand => Manager.EditCommand;
        public RelayCommand OpenUserManagerCommand => Manager.OpenUserManagerCommand;

        public ObservableCollection<UserCenterActivityDay> ActivityDays { get; } = new();

        public bool IsAdminUser => Authorization.Instance.PermissionMode <= PermissionMode.Administrator;
        public Visibility AdminButtonVisibility => IsAdminUser ? Visibility.Visible : Visibility.Collapsed;
        public Visibility LoggedInButtonVisibility => Manager.IsUserLoggedIn() ? Visibility.Visible : Visibility.Collapsed;
        public Visibility LoginButtonVisibility => Manager.IsUserLoggedIn() ? Visibility.Collapsed : Visibility.Visible;
        public string CurrentUserDisplay => Config.LoginResult?.User?.Username ?? "未登录";
        public string UserIdDisplay => Config.LoginResult?.User?.Id.ToString(CultureInfo.CurrentCulture) ?? "--";
        public string StatusDisplay => Config.LoginResult?.User is { } user ? (user.IsEnable ? "已启用" : "已停用") : "未知";
        public string PermissionModeDisplay => Config.LoginResult?.UserDetail?.PermissionMode.ToString() ?? "--";
        public string UserRoleDisplay
        {
            get
            {
                var loginResult = Config.LoginResult;
                if (loginResult?.Roles != null && loginResult.Roles.Any())
                    return string.Join(" · ", loginResult.Roles.Select(role => role.Name));
                return loginResult?.User?.Username != null ? "普通用户" : "未登录";
            }
        }
        public string ProfileSubtitle => $"{UserRoleDisplay}  ·  {StatusDisplay}";
        public string AccountUpdatedDisplay => Config.LoginResult?.User?.UpdatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? "--";

        public string CurrentSessionDisplay { get => _currentSessionDisplay; private set => SetField(ref _currentSessionDisplay, value); }
        private string _currentSessionDisplay = "0 分钟";
        public string TotalRunTimeDisplay { get => _totalRunTimeDisplay; private set => SetField(ref _totalRunTimeDisplay, value); }
        private string _totalRunTimeDisplay = "0 分钟";
        public string LaunchCountDisplay => Math.Max(0, Config.ApplicationLaunchCount).ToString("N0", CultureInfo.CurrentCulture);
        public string FirstLaunchDisplay => Config.FirstApplicationLaunchAt?.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) ?? "本次开始记录";
        public string LastLaunchDisplay => Config.LastApplicationLaunchAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? "--";

        public string TotalFlowCountDisplay { get => _totalFlowCountDisplay; private set => SetField(ref _totalFlowCountDisplay, value); }
        private string _totalFlowCountDisplay = "--";
        public string Last7DayFlowCountDisplay { get => _last7DayFlowCountDisplay; private set => SetField(ref _last7DayFlowCountDisplay, value); }
        private string _last7DayFlowCountDisplay = "--";
        public string CompletionRateDisplay { get => _completionRateDisplay; private set => SetField(ref _completionRateDisplay, value); }
        private string _completionRateDisplay = "--";
        public string AverageFlowDurationDisplay { get => _averageFlowDurationDisplay; private set => SetField(ref _averageFlowDurationDisplay, value); }
        private string _averageFlowDurationDisplay = "--";
        public string BusiestDayDisplay { get => _busiestDayDisplay; private set => SetField(ref _busiestDayDisplay, value); }
        private string _busiestDayDisplay = "--";
        public string ActiveDaysDisplay { get => _activeDaysDisplay; private set => SetField(ref _activeDaysDisplay, value); }
        private string _activeDaysDisplay = "--";
        public string ActivityRangeDisplay { get => _activityRangeDisplay; private set => SetField(ref _activityRangeDisplay, value); }
        private string _activityRangeDisplay = string.Empty;
        public string ActivitySummaryDisplay { get => _activitySummaryDisplay; private set => SetField(ref _activitySummaryDisplay, value); }
        private string _activitySummaryDisplay = "正在读取流程活动…";
        public string StatisticsStatusDisplay { get => _statisticsStatusDisplay; private set => SetField(ref _statisticsStatusDisplay, value); }
        private string _statisticsStatusDisplay = string.Empty;
        public Visibility StatisticsStatusVisibility => string.IsNullOrWhiteSpace(StatisticsStatusDisplay) ? Visibility.Collapsed : Visibility.Visible;
        public bool IsStatisticsLoading { get => _isStatisticsLoading; private set => SetField(ref _isStatisticsLoading, value); }
        private bool _isStatisticsLoading;

        public RbacManagerWindow()
        {
            Manager = RbacManager.GetInstance();
            InitializeComponent();
            _runtimeTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => RefreshRuntimeMetrics(), Dispatcher);
        }

        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            OnPropertyChanged(propertyName);
            if (propertyName == nameof(StatisticsStatusDisplay))
                OnPropertyChanged(nameof(StatisticsStatusVisibility));
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = this;

            if (!Manager.IsUserLoggedIn())
            {
                var loginWindow = CreateLoginWindow();
                bool? loginResult = loginWindow.ShowDialog();
                if (loginResult != true || !Manager.IsUserLoggedIn())
                {
                    Loaded += (_, _) => Close();
                    return;
                }
            }

            SetupPropertyChangeListener();
            Loaded += Window_Loaded;
            Closed += Window_Closed;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshRuntimeMetrics();
            _runtimeTimer.Start();
            _mysqlConnectionChangedHandler = (_, _) => Dispatcher.BeginInvoke(RefreshStatisticsAsync);
            MySqlControl.GetInstance().MySqlConnectChanged += _mysqlConnectionChangedHandler;
            await RefreshStatisticsAsync();
        }

        private void Window_Closed(object? sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _runtimeTimer.Stop();
            _statisticsCancellation?.Cancel();
            _statisticsCancellation?.Dispose();
            if (_configPropertyChangedHandler != null)
                Config.PropertyChanged -= _configPropertyChangedHandler;
            if (_mysqlConnectionChangedHandler != null)
                MySqlControl.GetInstance().MySqlConnectChanged -= _mysqlConnectionChangedHandler;
            GC.SuppressFinalize(this);
        }

        private LoginWindow CreateLoginWindow()
        {
            var loginWindow = new LoginWindow();
            var owner = GetShownOwner();
            if (owner != null)
            {
                loginWindow.Owner = owner;
                loginWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                loginWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            return loginWindow;
        }

        private Window? GetShownOwner()
        {
            if (Owner != null && Owner.IsVisible)
                return Owner;
            var activeWindow = Application.Current.GetActiveWindow();
            if (activeWindow != null && activeWindow != this && activeWindow.IsVisible)
                return activeWindow;
            return IsVisible ? this : null;
        }

        private void SetupPropertyChangeListener()
        {
            _configPropertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(RbacManagerConfig.LoginResult))
                    NotifyUserProperties();
            };
            Config.PropertyChanged += _configPropertyChangedHandler;
        }

        private void NotifyUserProperties()
        {
            OnPropertyChanged(nameof(Config));
            OnPropertyChanged(nameof(CurrentUserDisplay));
            OnPropertyChanged(nameof(UserIdDisplay));
            OnPropertyChanged(nameof(UserRoleDisplay));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(ProfileSubtitle));
            OnPropertyChanged(nameof(PermissionModeDisplay));
            OnPropertyChanged(nameof(AccountUpdatedDisplay));
            OnPropertyChanged(nameof(IsAdminUser));
            OnPropertyChanged(nameof(AdminButtonVisibility));
            OnPropertyChanged(nameof(LoggedInButtonVisibility));
            OnPropertyChanged(nameof(LoginButtonVisibility));
        }

        private void RefreshRuntimeMetrics()
        {
            CurrentSessionDisplay = FormatDuration(ApplicationUsageTracker.GetCurrentSessionDuration());
            TotalRunTimeDisplay = FormatDuration(ApplicationUsageTracker.GetTotalRunDuration());
            OnPropertyChanged(nameof(LaunchCountDisplay));
            OnPropertyChanged(nameof(FirstLaunchDisplay));
            OnPropertyChanged(nameof(LastLaunchDisplay));
        }

        private async Task RefreshStatisticsAsync()
        {
            _statisticsCancellation?.Cancel();
            _statisticsCancellation?.Dispose();
            _statisticsCancellation = new CancellationTokenSource();
            var cancellationToken = _statisticsCancellation.Token;
            IsStatisticsLoading = true;
            StatisticsStatusDisplay = string.Empty;

            var today = DateTime.Now.Date;
            var startDate = today.AddDays(-(ActivityDayCount - 1));
            ActivityRangeDisplay = $"{startDate:yyyy-MM-dd} — {today:yyyy-MM-dd}";
            try
            {
                var snapshot = await UserCenterStatisticsService.QueryAsync(startDate, today.AddDays(1), cancellationToken);
                ApplyStatistics(snapshot, startDate, today);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    IsStatisticsLoading = false;
            }
        }

        private void ApplyStatistics(UserCenterStatisticsSnapshot snapshot, DateTime startDate, DateTime today)
        {
            var summary = UserCenterStatisticsPresenter.Build(snapshot, startDate, today, ActivityDayCount);
            ActivityDays.Clear();
            foreach (var activityDay in summary.ActivityDays)
                ActivityDays.Add(activityDay);

            if (!summary.IsAvailable)
            {
                TotalFlowCountDisplay = "--";
                Last7DayFlowCountDisplay = "--";
                CompletionRateDisplay = "--";
                AverageFlowDurationDisplay = "--";
                BusiestDayDisplay = "--";
                ActiveDaysDisplay = "--";
                ActivitySummaryDisplay = "等待业务数据库连接";
                StatisticsStatusDisplay = summary.StatusMessage;
                return;
            }

            TotalFlowCountDisplay = summary.TotalExecutionCount.ToString("N0", CultureInfo.CurrentCulture);
            Last7DayFlowCountDisplay = summary.RecentExecutionCount.ToString("N0", CultureInfo.CurrentCulture);
            CompletionRateDisplay = summary.RecentCompletionRatePercent.HasValue ? $"{summary.RecentCompletionRatePercent:0.#}%" : "--";
            AverageFlowDurationDisplay = summary.AverageDurationMs.HasValue ? FormatFlowDuration(summary.AverageDurationMs.Value) : "--";
            BusiestDayDisplay = summary.BusiestDay is { } busiestDay
                ? $"{busiestDay.Date:MM-dd} · {busiestDay.ExecutionCount:N0} 次"
                : "暂无流程";
            ActiveDaysDisplay = $"{summary.ActiveDayCount} / {ActivityDayCount} 天";
            ActivitySummaryDisplay = $"近一年共执行 {summary.PeriodExecutionCount:N0} 次流程";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
            if (duration.TotalDays >= 1)
                return $"{(int)duration.TotalDays} 天 {duration.Hours} 小时";
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours} 小时 {duration.Minutes} 分";
            if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes} 分 {duration.Seconds} 秒";
            return $"{duration.Seconds} 秒";
        }

        private static string FormatFlowDuration(double milliseconds)
        {
            if (milliseconds >= 60_000)
                return $"{TimeSpan.FromMilliseconds(milliseconds):mm\\:ss}";
            if (milliseconds >= 1_000)
                return $"{milliseconds / 1_000:0.#} 秒";
            return $"{milliseconds:0} 毫秒";
        }

        private void BtnRefreshStatistics_Click(object sender, RoutedEventArgs e) => _ = RefreshStatisticsAsync();

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (!Manager.IsUserLoggedIn())
                return;
            var window = new ChangePasswordWindow(Config.LoginResult!.User!.Id)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.ShowDialog();
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要退出登录吗？", "退出登录", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            if (!string.IsNullOrEmpty(Config.SessionToken))
            {
                try
                {
                    await Manager.SessionService.RevokeSessionAsync(Config.SessionToken);
                }
                catch
                {
                }
            }

            try
            {
                if (Config.LoginResult?.User != null)
                {
                    await Manager.AuditLogService.AddAsync(
                        Config.LoginResult.User.Id,
                        Config.LoginResult.User.Username,
                        "user.logout",
                        $"用户退出登录，设备: {Environment.MachineName}");
                }
            }
            catch
            {
            }

            Config.LoginResult = new Dtos.LoginResultDto();
            Config.SessionToken = string.Empty;
            Config.RememberMe = false;
            Config.SavedUsername = string.Empty;
            Authorization.Instance.PermissionMode = PermissionMode.Guest;
            ConfigService.Instance.Save<RbacManagerConfig>();

            var loginWindow = new LoginWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            if (loginWindow.ShowDialog() == true && Manager.IsUserLoggedIn())
                NotifyUserProperties();
            else
                Close();
        }
    }
}
