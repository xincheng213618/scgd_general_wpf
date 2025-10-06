
using ColorVision.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.POI.POIGenCali
{
    /// <summary>
    /// EditSFR.xaml 的交互逻辑
    /// </summary>
    public partial class EditPoiGenCali : UserControl, ITemplateUserControl
    {
        public EditPoiGenCali()
        {
            InitializeComponent();
        }

        public void SetParam(object param)
        {
            this.DataContext = param;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            // X Configuration
            CobXGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>()
                                      select new KeyValuePair<GenType, string>(e1, e1.ToDescription());
            CobXGenType.SelectedIndex = 0;

            // Y Configuration
            CobYGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>()
                                      select new KeyValuePair<GenType, string>(e1, e1.ToDescription());
            CobYGenType.SelectedIndex = 0;

            // Z Configuration
            CobZGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>()
                                      select new KeyValuePair<GenType, string>(e1, e1.ToDescription());
            CobZGenType.SelectedIndex = 0;

            // x Configuration
            CobxGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>()
                                      select new KeyValuePair<GenType, string>(e1, e1.ToDescription());
            CobxGenType.SelectedIndex = 0;

            // y Configuration
            CobyGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>()
                                      select new KeyValuePair<GenType, string>(e1, e1.ToDescription());
            CobyGenType.SelectedIndex = 0;

            // u Configuration
            CobuGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>()
                                      select new KeyValuePair<GenType, string>(e1, e1.ToDescription());
            CobuGenType.SelectedIndex = 0;

            // v Configuration
            CobvGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>()
                                      select new KeyValuePair<GenType, string>(e1, e1.ToDescription());
            CobvGenType.SelectedIndex = 0;


            CobLabGenType.ItemsSource = from e1 in Enum.GetValues(typeof(GenType)).Cast<GenType>() 
                                        select new KeyValuePair<GenType, string>(e1, e1.ToDescription());
            CobLabGenType.SelectedIndex = 0;

            CobGenCalibrationType.ItemsSource = from e1 in Enum.GetValues(typeof(GenCalibrationType)).Cast<GenCalibrationType>()
                                                select new KeyValuePair<GenCalibrationType, string>(e1, e1.ToDescription());
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
