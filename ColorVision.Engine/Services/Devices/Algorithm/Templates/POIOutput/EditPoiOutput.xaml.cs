using ColorVision.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.PoiOutput
{
    /// <summary>
    /// EditLEDStripDetection.xaml 的交互逻辑
    /// </summary>
    public partial class EditPoiOutput : UserControl
    {
        public EditPoiOutput()
        {
            InitializeComponent();
        }
        public PoiOutputParam Param { get; set; }

        public void SetParam(PoiOutputParam param)
        {
            Param = param;
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
