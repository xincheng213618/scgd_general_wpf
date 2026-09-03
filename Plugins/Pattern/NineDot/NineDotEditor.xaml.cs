using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.NineDot
{
    /// <summary>
    /// NineDotEditor.xaml 的交互逻辑
    /// </summary>
    public partial class NineDotEditor : UserControl
    {
        public PatternNineDotConfig Config { get; }

        public NineDotEditor(PatternNineDotConfig patternNineDotConfig)
        {
            Config = patternNineDotConfig;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
