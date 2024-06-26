using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Themes;
using ColorVision.UI.Sorts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    /// <summary>
    /// WindowCVCIE.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCVCIE : Window
    {
        public ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }
        public WindowCVCIE(ObservableCollection<PoiResultCIExyuvData> poiResultCIExyuvDatas)
        {
            PoiResultCIExyuvDatas = poiResultCIExyuvDatas;
            InitializeComponent();
            this.ApplyCaption();
        }
        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void listViewSide_Initialized(object sender, EventArgs e)
        {
            var cieBdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y", "POIPointResultModel.ValidateResult" };
            var cieHeader = new List<string> { Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y", "Validate" };

            if (listViewSide.View is GridView gridViewPOI_XY_UV)
            {
                LeftGridViewColumnVisibilitys.Clear();
                gridViewPOI_XY_UV.Columns.Clear();
                for (int i = 0; i < cieHeader.Count; i++)
                    gridViewPOI_XY_UV.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
            }


            listViewSide.ItemsSource = PoiResultCIExyuvDatas;
        }

        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && listViewSide.View is GridView gridView && LeftGridViewColumnVisibilitys.Count == 0)
                GridViewColumnVisibility.GenContentMenuGridViewColumnZero(contextMenu, gridView.Columns, LeftGridViewColumnVisibilitys);
        }
    }
}
