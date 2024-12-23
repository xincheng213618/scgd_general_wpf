using System.Windows.Controls;

namespace ColorVision.Engine.Templates.LEDStripDetection
{
    /// <summary>
    /// EditSFR.xaml 的交互逻辑
    /// </summary>
    public partial class EditLEDStripDetection : UserControl,ITemplateUserControl
    {
        public EditLEDStripDetection()
        {
            InitializeComponent();
        }

        public void SetParam(object param)
        {
            this.DataContext = param;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {

        }
    }
}
