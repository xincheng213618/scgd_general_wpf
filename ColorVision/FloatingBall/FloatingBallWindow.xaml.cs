using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.FloatingBall
{
    /// <summary>
    /// FloatingBallWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FloatingBallWindow : Window
    {
        public FloatingBallWindow()
        {
            InitializeComponent();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if ( e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
             ContextMenu contextMenu = new ContextMenu();
            MenuItem menuItem1 = new MenuItem() { Header = "隐藏界面" };
            menuItem1.Click += (s, e) => { this.Close(); };
            contextMenu.Items.Add(menuItem1);


            MenuItem menuItem = new MenuItem() { Header ="退出程序" };
            menuItem.Click += (s, e) => { Environment.Exit(0); };
            contextMenu.Items.Add(menuItem);

            this.ContextMenu = contextMenu;
        }
    }
}
