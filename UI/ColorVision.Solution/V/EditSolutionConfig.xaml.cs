using ColorVision.Solution.V;
using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Util.Solution.V
{
    /// <summary>
    /// EditSolutionConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditSolutionConfig : Window
    {
        public SolutionExplorer SolutionExplorer { get; set; }
        private SolutionConfig SolutionConfig { get; set; }
        public EditSolutionConfig(SolutionExplorer solutionExplorer)
        {
            SolutionExplorer = solutionExplorer;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SolutionExplorer;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }
    }
}
