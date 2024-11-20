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
            this.ApplyCaption(false);
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

            SNtextBox.Focus();
        }


        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {

        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        private void listView1_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }
    }
}