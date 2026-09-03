using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.Checkerboard
{
    /// <summary>
    /// CheckerboardEditor.xaml 的交互逻辑
    /// </summary>
    public partial class CheckerboardEditor : UserControl
    {
        public PatternCheckerboardConfig Config { get; }

        public CheckerboardEditor(PatternCheckerboardConfig patternCheckerboardConfig)
        {
            Config = patternCheckerboardConfig;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
