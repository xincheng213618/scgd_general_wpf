using ColorVision.Themes;
using ColorVision.UI.Menus;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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