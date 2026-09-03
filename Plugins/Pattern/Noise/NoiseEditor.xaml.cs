using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.Noise
{
    /// <summary>
    /// NoiseEditor.xaml 的交互逻辑
    /// </summary>
    public partial class NoiseEditor : UserControl
    {
        public PatternNoiseConfig Config { get; }

        public NoiseEditor(PatternNoiseConfig config)
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
