using ColorVision.Common.Extension;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Shell
{
    public class TrayIconManager : IDisposable
    {
        private static TrayIconManager _instance;
        private static readonly object _locker = new();
        public static TrayIconManager GetInstance() { lock (_locker) { return _instance ??= new TrayIconManager(); } }

        private System.Windows.Forms.NotifyIcon _notifyIcon;

        private ContextMenu _contextMenu;

        public TrayIconManager()
        {
            InitializeNotifyIcon();
            InitializeContextMenu();

        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ResourceAssembly.Location),
                Visible = true,
                Text = "ColorVisino"
            };

            //使用winform
            //var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            //contextMenu.Items.Add("Show", null, Show_Click);
            //contextMenu.Items.Add("Exit", null, Exit_Click);
            //_notifyIcon.ContextMenuStrip = contextMenu;
            //_notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            //使用wpf 的ContextMenu
            _notifyIcon.MouseUp += NotifyIcon_MouseUp;

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
        private void NotifyIcon_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                _contextMenu.IsOpen = true;
            }
        }
        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void Show_Click(object sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void Exit_Click(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            Application.Current.Shutdown();
        }

        private void ShowMainWindow()
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
