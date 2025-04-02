using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.SFR
{
    /// <summary>
    /// EditSFR.xaml 的交互逻辑
    /// </summary>
    public partial class EditSFR : UserControl,ITemplateUserControl
    {
        public EditSFR()
        {
            InitializeComponent();
        }
        public SFRParam Param { get; set; }

        public void SetParam(object param)
        {
            if (param is SFRParam sFRParam)
            {
                Param = sFRParam;
            }
            this.DataContext = Param;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
        }

        private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

    }
}
