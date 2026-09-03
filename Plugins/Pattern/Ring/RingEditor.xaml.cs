using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.Ring
{
    /// <summary>
    /// RingEditor.xaml 的交互逻辑
    /// </summary>
    public partial class RingEditor : UserControl
    {
        public PatternRingConfig Config { get; }

        public RingEditor(PatternRingConfig patternRingConfig)
        {
            Config = patternRingConfig;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
