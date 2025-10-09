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
            
            // 如果没有登录用户，显示默认管理员信息提示
            var rbacManager = RbacManager.GetInstance();
            this.DataContext = rbacManager;

            if (rbacManager.Config.LoginResult?.User?.Username == null)
            {
                // 可以在这里设置一个默认的显示状态，提示用户登录
                // 暂时保留当前逻辑，显示"未登录"
            }
            
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
                    }
                };
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
