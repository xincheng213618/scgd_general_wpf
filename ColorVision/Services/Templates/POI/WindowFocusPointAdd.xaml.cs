using ColorVision.Themes.Controls;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Templates.POI
{
    /// <summary>
    /// WindowFocusPointAdd.xaml 的交互逻辑
    /// </summary>
    public partial class WindowFocusPointAdd : BaseWindow
    {
        public ObservableCollection<TemplateModelBase> ListConfigs { get; set; }
        public WindowFocusPointAdd(ObservableCollection<TemplateModelBase> ListConfigs )
        {
            this.ListConfigs = ListConfigs;
            InitializeComponent();
            ListView1.ItemsSource = ListConfigs;
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
                if (ListConfigs[ListView1.SelectedIndex].GetValue() is PoiParam poiParam)
                SelectPoiParam = poiParam;
                Close();
            }
        }
    }
}
