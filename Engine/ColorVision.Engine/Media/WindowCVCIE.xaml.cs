using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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
        [DisplayName("是否显示记录数据")]
        public bool IsShowString { get => _IsShowString; set { _IsShowString = value; OnPropertyChanged(); } }
        private bool _IsShowString = true;
        [DisplayName("数据显示模板")]
        public string Template { get => _Template;set { _Template = value;  OnPropertyChanged(); } }
        private string _Template = "X:@X:F1 Y:@Y:F1 Z:@Z:F1\\nx:@x:F4 y:@y:F4 u:@u:F4 v:@v:F4\\nCCT:@CCT:F1 Wave:@Wave:F1";
    }

    /// <summary>
    /// WindowCVCIE.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCVCIE : Window
    {
        public ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }

        bool IsPoiResultCIExyuvDatas = true;
        public WindowCVCIE(ObservableCollection<PoiResultCIExyuvData> poiResultCIExyuvDatas)
        {
            IsPoiResultCIExyuvDatas = true;
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
        public ObservableCollection<PoiResultCIEYData> PoiResultCIEYDatas { get; set; }

        public WindowCVCIE(ObservableCollection<PoiResultCIEYData> poiResultCIEYDatas)
        {
            IsPoiResultCIExyuvDatas = false;
            PoiResultCIEYDatas = poiResultCIEYDatas;
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

            listViewSide.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listViewSide.SelectAll(), (s, e) => e.CanExecute = true));
            listViewSide.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));
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
            if (IsPoiResultCIExyuvDatas)
            {
                PoiResultCIExyuvDatas.SaveCsv(dialog.FileName);
            }
            else
            {
                PoiResultCIEYData.SaveCsv(PoiResultCIEYDatas, dialog.FileName);
            }
        }


    }
}
