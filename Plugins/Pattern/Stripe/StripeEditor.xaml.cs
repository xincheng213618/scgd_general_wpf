using ColorVision.UI;
using System.Windows.Controls;

namespace Pattern.Stripe
{
    /// <summary>
    /// StripeEditor.xaml 的交互逻辑
    /// </summary>
    public partial class StripeEditor : UserControl
    {
        public PatternStripeConfig Config { get; }

        public StripeEditor(PatternStripeConfig patternStripeConfig)
        {
            Config = patternStripeConfig;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
    }
}
