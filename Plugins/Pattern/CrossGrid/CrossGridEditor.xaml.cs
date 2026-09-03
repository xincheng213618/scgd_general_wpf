using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.CrossGrid
{
    /// <summary>
    /// CrossGridEditor.xaml 的交互逻辑
    /// </summary>
    public partial class CrossGridEditor : UserControl
    {
        public PatternCrossGridConfig Config { get; }

        public CrossGridEditor(PatternCrossGridConfig patternCrossGridConfig)
        {
            Config = patternCrossGridConfig;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
