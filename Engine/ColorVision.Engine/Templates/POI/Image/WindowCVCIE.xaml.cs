using ColorVision.Engine.Templates.POI;
using ColorVision.Themes;
using ColorVision.UI.Sorts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Media
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
            var cieHeader = new List<string> { Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "CCT", "Wave", "X", "Y", "Z", "u'", "v'", "x", "y", "Validate" };

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            PoiResultCIExyuvData.SaveCsv(PoiResultCIExyuvDatas, dialog.FileName);
        }
    }
}
