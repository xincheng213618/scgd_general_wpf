using ColorVision.Themes;
using log4net;
using System.Windows;
using System.Linq;

namespace ProjectKB
{
    /// <summary>
    /// Interaction logic for ProjectKBWindow.xaml
    /// </summary>
    public partial class ProjectKBWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectKBWindow));

        public ProjectKBWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ProjectKBConfig.Instance;
        }

        private void TestClick(object sender, RoutedEventArgs e)
        {
            Random random = new Random();
            string outstr = string.Empty;
            foreach (var item in Enum.GetValues(typeof(System.Windows.Input.Key)).Cast<System.Windows.Input.Key>())
            {
                string formattedString = $"[{item}]";

                outstr += $"{formattedString ,-20}   {random.NextDouble():F4}   {random.NextDouble():F4}   {random.NextDouble() * 100:F2}%" + Environment.NewLine;
            }
            outputText.Text = outstr;
        }

        private void Button_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }
    }
}