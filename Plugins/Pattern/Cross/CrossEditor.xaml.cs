using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.Cross
{
    /// <summary>
    /// CrossEditor.xaml 的交互逻辑
    /// </summary>
    public partial class CrossEditor : UserControl
    {
        public PatternCrossConfig Config { get; }

        public CrossEditor(PatternCrossConfig patternCrossConfig)
        {
            Config = patternCrossConfig;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
