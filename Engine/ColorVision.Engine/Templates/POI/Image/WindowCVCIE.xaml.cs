using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.PropertyEditor;
using ColorVision.UI.Sorts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Media
{
    public class CVCIEShowConfig : ViewModelBase, IConfig
    {
        public static CVCIEShowConfig Instance => ConfigService.Instance.GetRequiredService<CVCIEShowConfig>();
        public RelayCommand EditCommand { get; set; }

        public CVCIEShowConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        public bool IsShowString { get => _IsShowString; set { _IsShowString = value; NotifyPropertyChanged(); } }
        private bool _IsShowString = true;

        public string Template { get => _Template;set { _Template = value; } }
        private string _Template = "X:@X:F1 Y:@Y:F1 Z:@Z:F1\\nx:@x:F4 y:@y:F4 u:@u:F4 v:@v:F4\\nCCT:@CCT:F1 Wave:@Wave:F1";
    }

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

            var cieBdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };
            var cieHeader = new List<string> { Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "CCT", "Wave", "X", "Y", "Z", "u'", "v'", "x", "y" };

            if (listViewSide.View is GridView gridViewPOI_XY_UV)
            {
                LeftGridViewColumnVisibilitys.Clear();
                gridViewPOI_XY_UV.Columns.Clear();
                for (int i = 0; i < cieHeader.Count; i++)
                    gridViewPOI_XY_UV.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
            }
            listViewSide.ItemsSource = PoiResultCIExyuvDatas;
        }

        public WindowCVCIE(ObservableCollection<PoiResultCIEYData> poiResultCIEYDatas)
        {
            InitializeComponent();
            this.ApplyCaption();

            var cieBdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "Y"};
            var cieHeader = new List<string> { Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Size, Properties.Resources.Shape, "Y" };

            if (listViewSide.View is GridView gridViewPOI_XY_UV)
            {
                LeftGridViewColumnVisibilitys.Clear();
                gridViewPOI_XY_UV.Columns.Clear();
                for (int i = 0; i < cieHeader.Count; i++)
                    gridViewPOI_XY_UV.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
            }
            listViewSide.ItemsSource = poiResultCIEYDatas;
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = CVCIEShowConfig.Instance;

        }

        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();


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
