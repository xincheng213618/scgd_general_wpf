using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.Solid
{
    /// <summary>
    /// SolidEditor.xaml 的交互逻辑
    /// </summary>
    public partial class SolidEditor : UserControl
    {
        public PatternSolodConfig Config { get; }

        public SolidEditor(PatternSolodConfig patternSolodConfig)
        {
            Config = patternSolodConfig;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
