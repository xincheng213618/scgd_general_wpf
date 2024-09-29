using ColorVision.Engine.Templates.POI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIFilters
{
    /// <summary>
    /// EditLEDStripDetection.xaml 的交互逻辑
    /// </summary>
    public partial class EditPoiFilters : UserControl
    {
        public EditPoiFilters()
        {
            InitializeComponent();
        }
        public PoiFilterParam Param { get; set; }

        public void SetParam(PoiFilterParam param)
        {
            Param = param;
            this.DataContext = Param;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            ComboBoxXYZType.ItemsSource = from e1 in Enum.GetValues(typeof(XYZType)).Cast<XYZType>() select new KeyValuePair<int, string>((int)e1, e1.ToString());
            ComboBoxXYZType.SelectedIndex = 0;
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
