using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.Dot
{
    /// <summary>
    /// DotEditor.xaml 的交互逻辑
    /// </summary>
    public partial class DotEditor : UserControl
    {
        public PatternDotConfig Config { get; }

        public DotEditor(PatternDotConfig config)
        {
            Config = config;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
