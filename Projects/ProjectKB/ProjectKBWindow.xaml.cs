using ColorVision.Themes;
using log4net;
using System.Windows;
using System.Linq;
using ColorVision.Common.MVVM;

namespace ProjectKB
{
    public class KBItem : ViewModelBase
    {
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;

        public double Exposure { get => _Exposure; set { _Exposure = value; NotifyPropertyChanged(); } }
        private double _Exposure;

        public double AvgLv { get => _AvgLv; set { _AvgLv = value; NotifyPropertyChanged(); } }
        private double _AvgLv;

        public double AvgC1 { get => _AvgC1; set { _AvgC1 = value; NotifyPropertyChanged(); } }
        private double _AvgC1;

        public double AvgC2 { get => _AvgC2; set { _AvgC2 = value; NotifyPropertyChanged(); } }
        private double _AvgC2;

        public double MinLv { get => _MinLv; set { _MinLv = value; NotifyPropertyChanged(); } }
        private double _MinLv;
    }

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