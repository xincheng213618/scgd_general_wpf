using ColorVision.Common.MVVM;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Shell
{
    public class TrayIconConfig : ViewModelBase, IConfig, IConfigSettingProvider
    {
        public static TrayIconConfig Instance => ConfigService.Instance.GetRequiredService<TrayIconConfig>();

        public bool IsShowTrayIcon { get => _IsShowTrayIcon; set { _IsShowTrayIcon = value; OnPropertyChanged(); } }
        private bool _IsShowTrayIcon;

        public bool IsUseWPFContextMenu { get => _IsUseWPFContextMenu; set { _IsUseWPFContextMenu = value; OnPropertyChanged(); } }
        private bool _IsUseWPFContextMenu = true;

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            if (!TrayIconManager.IsInitialized) return new List<ConfigSettingMetadata>();
            return new List<ConfigSettingMetadata>()
            {
                new ConfigSettingMetadata()
                {
                    Order = 99,
                    BindingName =nameof(IsShowTrayIcon),
                    Source = Instance,
                },
                new ConfigSettingMetadata()
                {
                    Order = 99,
                    BindingName =nameof(IsUseWPFContextMenu),
                    Source = Instance,
                },
            };
        }
    }


    public class TrayIconManager : IDisposable
    {
        public static bool IsInitialized { get; private set; }
        private static TrayIconManager _instance;
        private static readonly object _locker = new();
        public static TrayIconManager GetInstance() { lock (_locker) { return _instance ??= new TrayIconManager(); } }

        private System.Windows.Forms.NotifyIcon _notifyIcon;

        private ContextMenu _contextMenu;

        public TrayIconManager()
        {
            Initialized();
            TrayIconConfig.Instance.PropertyChanged += (s, e) => Initialized();
            IsInitialized = true;
        }

        public void Initialized()
        {
            _notifyIcon?.Dispose();
            if (TrayIconConfig.Instance.IsShowTrayIcon)
            {
                InitializeNotifyIcon();
                InitializeContextMenu();
            }
        }


        private void InitializeNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ResourceAssembly.Location),
                Visible = true,
                Text = "ColorVisino"
            };

            if (TrayIconConfig.Instance.IsUseWPFContextMenu)
            {
                _notifyIcon.MouseUp += NotifyIcon_MouseUp;
            }
            else
            {
                //使用winform
                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("Show", null, Show_Click);
                contextMenu.Items.Add("Exit", null, Exit_Click);
                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            }
        }


        private void InitializeContextMenu()
        {
            _contextMenu = new ContextMenu();

            var showMenuItem = new MenuItem { Header = "Show" };
            showMenuItem.Click += Show_Click;
            _contextMenu.Items.Add(showMenuItem);

            var exitMenuItem = new MenuItem { Header = "Exit" };
            exitMenuItem.Click += Exit_Click;
            _contextMenu.Items.Add(exitMenuItem);
        }
        private void NotifyIcon_MouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                _contextMenu.IsOpen = true;
            }
        }
        private void NotifyIcon_DoubleClick(object?  sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void Show_Click(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void Exit_Click(object? sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            Application.Current.Shutdown();
        }

        private static void ShowMainWindow()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }
            mainWindow.Show();
            mainWindow.Activate();
        }

        public void Dispose()
        {
            _notifyIcon.Dispose();
            GC.SuppressFinalize(this);
            
        }
    }
}
