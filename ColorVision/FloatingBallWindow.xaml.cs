#pragma warning disable CS8602
using ColorVision.UI;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace ColorVision.FloatingBall
{

    public class FloatingBallWindowConfig : WindowConfig
    {

    }

    /// <summary>
    /// FloatingBallWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FloatingBallWindow : Window
    {
        public static FloatingBallWindowConfig Config => ConfigService.Instance.GetRequiredService<FloatingBallWindowConfig>();


        public FloatingBallWindow()
        {
            InitializeComponent();
            if (Config.Top == 0)
            {
                // 获取主屏幕
                var screen = Screen.PrimaryScreen;
                // 获取主屏幕工作区（物理像素）
                var workingArea = screen.WorkingArea;

                // 获取当前显示器 DPI
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0, dpiY = 1.0;
                if (source != null)
                {
                    dpiX = source.CompositionTarget.TransformFromDevice.M11;
                    dpiY = source.CompositionTarget.TransformFromDevice.M22;
                }
                else
                {
                    // 新窗口未显示时，用默认主屏幕 DPI
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        dpiX = 96.0 / g.DpiX;
                        dpiY = 96.0 / g.DpiY;
                    }
                }

                // 物理像素 -> DIP（WPF 坐标）
                Config.Left = workingArea.Right * dpiX - this.Width - 100;
                Config.Top = workingArea.Top * dpiY + 100;
            }
            Config.SetWindow(this);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
                Config.SetConfig(this);
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ContextMenu contextMenu = new();
            MenuItem menuItem1 = new() { Header = "隐藏界面" };
            menuItem1.Click += (s, e) => { Close(); };
            contextMenu.Items.Add(menuItem1);


            MenuItem menuItem = new() { Header ="退出程序" };
            menuItem.Click += (s, e) => { Environment.Exit(0); };
            contextMenu.Items.Add(menuItem);

            ContextMenu = contextMenu;
        }
    }
}
