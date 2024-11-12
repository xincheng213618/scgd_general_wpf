using ColorVision.Themes;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MenuManager.GetInstance().Menu = Menu1;
        }
    }
}