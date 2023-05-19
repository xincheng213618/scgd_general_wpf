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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CvOperator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.AllScreens[0];
            InitializeComponent();
            //工作区域
            //Left = screen.WorkingArea.Left;
            //Top = screen.WorkingArea.Top;
            //Height = screen.WorkingArea.Height;
            //Width = screen.WorkingArea.Width;
            Left = screen.WorkingArea.Left;
            Top = screen.WorkingArea.Top;
            this.Width = System.Windows.SystemParameters.PrimaryScreenWidth;
            this.Height = System.Windows.SystemParameters.PrimaryScreenHeight;
            ResizeMode  =ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
