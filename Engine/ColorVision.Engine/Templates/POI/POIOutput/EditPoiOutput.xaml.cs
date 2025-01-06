using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.POI.POIOutput
{


    public partial class EditPoiOutput : UserControl, ITemplateUserControl
    {
        public EditPoiOutput()
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
