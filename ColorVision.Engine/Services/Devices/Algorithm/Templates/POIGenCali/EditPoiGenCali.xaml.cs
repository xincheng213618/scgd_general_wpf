using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POIGenCali
{
    /// <summary>
    /// EditLEDStripDetection.xaml 的交互逻辑
    /// </summary>
    public partial class EditPoiGenCali : UserControl
    {
        public EditPoiGenCali()
        {
            InitializeComponent();
        }
        public PoiGenCaliParam Param { get; set; }

        public void SetParam(PoiGenCaliParam param)
        {
            Param = param;
            this.DataContext = Param;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            CobXYZGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>() select new KeyValuePair<GenType, string>(e1, e1.ToString());
            CobXYZGenType.SelectedIndex = 0;

            CobxyGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>() select new KeyValuePair<GenType, string>(e1, e1.ToString());
            CobxyGenType.SelectedIndex = 0;

            CobuvGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>() select new KeyValuePair<GenType, string>(e1, e1.ToString());
            CobuvGenType.SelectedIndex = 0;

            CobLabGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>() select new KeyValuePair<GenType, string>(e1, e1.ToString());
            CobLabGenType.SelectedIndex = 0;

            CobGenCalibrationType.ItemsSource = from e1 in Enum.GetValues(typeof(GenCalibrationType)).Cast<GenCalibrationType>() select new KeyValuePair<GenCalibrationType, string>(e1, e1.ToString());
            CobGenCalibrationType.SelectedIndex = 0;
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
