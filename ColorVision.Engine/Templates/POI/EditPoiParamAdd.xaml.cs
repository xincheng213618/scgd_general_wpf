using ColorVision.Engine.MySql;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// EditPoiParamAdd.xaml 的交互逻辑
    /// </summary>
    public partial class EditPoiParamAdd : Window
    {
        PoiParam PoiParam { get; set; }

        public EditPoiParamAdd(PoiParam poiParam)
        {
            PoiParam = poiParam;
            InitializeComponent();
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }
        public PoiParam SelectPoiParam { get; set; }
        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                SelectPoiParam = TemplatePoi.Params[ListView1.SelectedIndex].Value;
                PoiParam.LoadPoiDetailFromDB(SelectPoiParam);
                PoiParam.PoiPoints.Clear();
                foreach (var item in SelectPoiParam.PoiPoints)
                {
                    item.Id = -1;
                    PoiParam.PoiPoints.Add(item);
                }
                SelectPoiParam.PoiPoints.Clear();
                MessageBox.Show("导入成功", "ColorVision");
            }
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {

            ListView1.ItemsSource = TemplatePoi.Params;
        }
    }
}
