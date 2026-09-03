using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.QuadrantGrating
{
    public partial class QuadrantGratingEditor : UserControl
    {
        public PatternQuadrantGratingConfig Config { get; }

        public QuadrantGratingEditor(PatternQuadrantGratingConfig config)
        {
            Config = config;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
