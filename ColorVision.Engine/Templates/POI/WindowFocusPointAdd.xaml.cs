using ColorVision.Engine.MySql;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.POI
{
    /// <summary>
    /// WindowFocusPointAdd.xaml 的交互逻辑
    /// </summary>
    public partial class WindowFocusPointAdd : Window
    {
        PoiParam PoiParam { get; set; }

        public WindowFocusPointAdd(PoiParam poiParam)
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
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                    PoiParam.LoadPoiDetailFromDB(SelectPoiParam);

                foreach (var item in SelectPoiParam.PoiPoints)
                {
                    PoiParam.PoiPoints.Add(item);
                }
                MessageBox.Show("导入成功", "ColorVision");
            }
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {

            ListView1.ItemsSource = TemplatePOI.Params;
        }
    }
}
