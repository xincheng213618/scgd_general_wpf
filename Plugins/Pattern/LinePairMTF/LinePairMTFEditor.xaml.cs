using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.LinePairMTF
{
    /// <summary>
    /// LinePairMTFEditor.xaml 的交互逻辑
    /// </summary>
    public partial class LinePairMTFEditor : UserControl
    {
        public PatternLinePairMTFConfig Config { get; }

        public LinePairMTFEditor(PatternLinePairMTFConfig config)
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
