using ColorVision.Common.Sorts;
using ColorVision.Services.PhyCameras.Dao;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.PhyCameras
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class InfoPhyCamera : UserControl
    {
        public PhyCamera Device { get; set; }

        public bool IsCanEdit { get; set; }
        public InfoPhyCamera(PhyCamera deviceCamera,bool isCanEdit =true)
        {
            Device = deviceCamera;
            IsCanEdit = isCanEdit;
            InitializeComponent();
        }

        public ObservableCollection<CameraLicenseModel> LicenseModels { get; set; } = new ObservableCollection<CameraLicenseModel>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            DataContext = Device;
        }


        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show("数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (control.Tag?.ToString() ?? string.Empty)
                {
                    case "Calibration":
                        break;
                }
            }
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ListViewLincense.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumnZero(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        switch (item.ColumnName)
                        {
                            case "序号":
                                item.IsSortD = !item.IsSortD;
                                LicenseModels.SortByID(item.IsSortD);
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                }
            }
        }


        private void TextBlock_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                NativeMethods.Clipboard.SetText(textBlock.Text);
                MessageBox.Show(textBlock.Text);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}
