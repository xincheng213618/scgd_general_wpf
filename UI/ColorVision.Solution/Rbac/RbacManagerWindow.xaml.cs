using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Rbac
{
    public class MenuRbacManager : IRightMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
            menuItemMetadata.Command = new RelayCommand(a => new RbacManagerWindow() {  Owner =Application.Current.GetActiveWindow(),WindowStartupLocation =WindowStartupLocation.CenterOwner}.ShowDialog() );
            menuItemMetadata.Icon = new Image()
            {
                Source = (ImageSource)Application.Current.Resources["DrawingImageUser"],
            };
            return new MenuItemMetadata[] { menuItemMetadata };
        }
    }

    /// <summary>
    /// RbacManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RbacManagerWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsAdminUser
        {
            get
            {
                return Authorization.Instance.PermissionMode <= PermissionMode.Administrator;
            }
        }

        public System.Windows.Visibility AdminButtonVisibility
        {
            get
            {
                return IsAdminUser ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public System.Windows.Visibility LoggedInButtonVisibility
        {
            get
            {
                var rbacManager = RbacManager.GetInstance();
                return rbacManager.IsUserLoggedIn() ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public System.Windows.Visibility LoginButtonVisibility
        {
            get
            {
                var rbacManager = RbacManager.GetInstance();
                return rbacManager.IsUserLoggedIn() ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
        }

        public string CurrentUserDisplay
        {
            get
            {
                var rbacManager = RbacManager.GetInstance();
                var loginResult = rbacManager.Config.LoginResult;
                return loginResult?.User?.Username ?? "未登录";
            }
            set { }
        }

        public string StatusDisplay
        {
            get
            {
                var rbacManager = RbacManager.GetInstance();
                var loginResult = rbacManager.Config.LoginResult;
                
                if (loginResult?.User != null)
                {
                    return loginResult.User.IsEnable ? "启用" : "禁用";
                }
                return "未知";
            }
            set { }
        }

        public string UserRoleDisplay
        {
            get
            {
                var rbacManager = RbacManager.GetInstance();
                var loginResult = rbacManager.Config.LoginResult;
                
                if (loginResult?.Roles != null && loginResult.Roles.Any())
                {
                    return string.Join(", ", loginResult.Roles.Select(r => r.Name));
                }
                return loginResult?.User?.Username != null ? "普通用户" : "未登录";
            }
            set { }
        }

        public RbacManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        
        private void Window_Initialized(object sender, EventArgs e)
        {
            var rbacManager = RbacManager.GetInstance();
            this.DataContext = rbacManager;

            // 未登录时，先弹出登录窗口
            if (!rbacManager.IsUserLoggedIn())
            {
                var loginWindow = new LoginWindow()
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                bool? loginResult = loginWindow.ShowDialog();
                if (loginResult != true || !rbacManager.IsUserLoggedIn())
                {
                    // 登录取消或失败，关闭管理窗口
                    this.Loaded += (s, args) => this.Close();
                    return;
                }
            }

            SetupPropertyChangeListener(rbacManager);
        }

        private void SetupPropertyChangeListener(RbacManager rbacManager)
        {
            // 监听登录状态变化
            if (rbacManager.Config is INotifyPropertyChanged config)
            {
                config.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(RbacManagerConfig.LoginResult))
                    {
                        OnPropertyChanged(nameof(CurrentUserDisplay));
                        OnPropertyChanged(nameof(UserRoleDisplay));
                        OnPropertyChanged(nameof(StatusDisplay));
                        OnPropertyChanged(nameof(IsAdminUser));
                        OnPropertyChanged(nameof(AdminButtonVisibility));
                        OnPropertyChanged(nameof(LoggedInButtonVisibility));
                        OnPropertyChanged(nameof(LoginButtonVisibility));
                    }
                };
            }
        }

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var rbacManager = RbacManager.GetInstance();
            if (!rbacManager.IsUserLoggedIn()) return;

            var userId = rbacManager.Config.LoginResult!.User!.Id;
            var window = new ChangePasswordWindow(userId)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要退出登录吗？", "退出登录", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var rbacManager = RbacManager.GetInstance();
                
                // 撤销当前会话
                if (!string.IsNullOrEmpty(rbacManager.Config.SessionToken))
                {
                    try
                    {
                        await rbacManager.SessionService.RevokeSessionAsync(rbacManager.Config.SessionToken);
                    }
                    catch { }
                }

                // 记录审计日志
                try
                {
                    if (rbacManager.Config.LoginResult?.User != null)
                    {
                        await rbacManager.AuditLogService.AddAsync(
                            rbacManager.Config.LoginResult.User.Id,
                            rbacManager.Config.LoginResult.User.Username,
                            "user.logout",
                            $"用户退出登录，设备: {Environment.MachineName}");
                    }
                }
                catch { }

                // 清除登录信息
                rbacManager.Config.LoginResult = new Dtos.LoginResultDto();
                rbacManager.Config.SessionToken = string.Empty;
                
                // 清除持久化的凭据
                rbacManager.Config.RememberMe = false;
                rbacManager.Config.SavedUsername = string.Empty;
                
                // 重置权限
                Authorization.Instance.PermissionMode = UI.Authorizations.PermissionMode.Guest;
                
                // 更新UI
                OnPropertyChanged(nameof(CurrentUserDisplay));
                OnPropertyChanged(nameof(UserRoleDisplay));
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(IsAdminUser));
                OnPropertyChanged(nameof(AdminButtonVisibility));
                OnPropertyChanged(nameof(LoggedInButtonVisibility));
                OnPropertyChanged(nameof(LoginButtonVisibility));
                
                MessageBox.Show("已成功退出登录", "退出", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
