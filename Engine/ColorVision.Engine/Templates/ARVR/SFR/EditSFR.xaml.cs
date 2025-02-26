using ColorVision.Engine.Templates.POI;
using System.Windows;
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
            ComboBoxPoi.ItemsSource = TemplatePoi.Params;
        }

        private void TextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!int.TryParse(TextIndex.Text, out int index)) return;
            if (ComboBoxPoi.SelectedValue is PoiParam poiParam)
            {
                poiParam.LoadPoiDetailFromDB();

                if (0<= index && index <  poiParam.PoiPoints.Count)
                {
                    var PoiPoint = poiParam.PoiPoints[index];
                    Param.RECT =  new System.Windows.Rect(PoiPoint.PixX - PoiPoint.PixWidth/2, PoiPoint.PixY - PoiPoint.PixHeight / 2, PoiPoint.PixWidth /2, PoiPoint.PixHeight);
                }
                else
                {
                    MessageBox.Show("Index out of range");
                }
            }
            else
            {
                MessageBox.Show("Please select a POI and input a valid index");
            }

        }
    }
}
