using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Devices.Camera.Dao;
using ColorVision.Services.Devices.Spectrum.Views;
using ColorVision.Services.Interfaces;
using ColorVision.Services.Msg;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.Sorts;
using ColorVision.Templates;
using ColorVision.Themes.Controls;

namespace ColorVision.Services.Devices.Camera
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCameraControl : UserControl
    {
        public DeviceCamera Device { get; set; }

        public MQTTCamera DService { get => Device.DeviceService; }

        public bool IsCanEdit { get; set; }

        public DeviceCameraControl(DeviceCamera deviceCamera,bool isCanEdit =true)
        {
            Device = deviceCamera;
            IsCanEdit = isCanEdit;
            InitializeComponent();
            this.Loaded += DeviceCameraControl_Loaded;
        }

        private void DeviceCameraControl_Loaded(object sender, RoutedEventArgs e)
        {
            Device.IsEditMode = false;
        }

        public ObservableCollection<CameraLicenseModel> LicenseModels { get; set; } = new ObservableCollection<CameraLicenseModel>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = Device;
            if (IsCanEdit)
            {
                UserControl userControl = Device.GetEditControl();
                if (userControl.Parent is Panel grid)
                    grid.Children.Remove(userControl);
                MQTTEditContent.Children.Add(userControl);
            }

            Device.RefreshLincense();
            ListViewLincense.ItemsSource = Device.LicenseModels;

            var lists = Enum.GetValues(typeof(ResouceType)).Cast<ResouceType>();
            foreach (var item in lists)
            {

                TabItem tabItem = new TabItem();
                tabItem.Header = item.ToDescription();

                StackPanel stackPanel = new StackPanel();
                tabItem.Content = stackPanel;

                ListView listView = new ListView();

                GridView gridView = new GridView();
                gridView.Columns.Add(new GridViewColumn() { Header = "序号", DisplayMemberBinding = new Binding("Id") });
                gridView.Columns.Add(new GridViewColumn() { Header = "名称", DisplayMemberBinding = new Binding("Name") });
                gridView.Columns.Add(new GridViewColumn() { Header = "路径", DisplayMemberBinding = new Binding("FilePath") });
                listView.View = gridView;
                stackPanel.Children.Add(listView);
                ObservableCollection<CalibrationResource> CalibrationRsources = new ObservableCollection<CalibrationResource>();

                foreach (var resourceObject in Device.VisualChildren)
                {
                    if (resourceObject is CalibrationResource calibrationResource && calibrationResource.SysResourceModel.Type ==(int)item)
                    {
                        CalibrationRsources.Add(calibrationResource);
                    }
                }

                listView.ItemsSource = CalibrationRsources;


                Popup orderPopup = new Popup { Name = "OrderPopup", AllowsTransparency = true, Focusable = false, PopupAnimation = PopupAnimation.Slide, Placement = PlacementMode.Bottom, StaysOpen = false };
                Button orderButton = new Button  { Name = "Order",Content = "排序", Margin = new Thickness(5) };
                orderButton.Click += (s, e) =>
                {
                    orderPopup.IsOpen = true;
                };

                orderPopup.PlacementTarget = orderButton;

                // 创建边框和堆栈面板
                Border border = new Border  {  Margin = new Thickness(5),Width = 80 };
                border.Style = this.FindResource("BorderModuleArea") as Style;

                StackPanel stackPanelorder = new StackPanel  {  Margin = new Thickness(5) };

                StackPanel stackPanelorder1 = new StackPanel { Margin = new Thickness(0,5,0,5) };
                StackPanel stackPanelorder2 = new StackPanel { Margin = new Thickness(0,5,0,5) };
                stackPanelorder.Children.Add(stackPanelorder1);
                stackPanelorder.Children.Add(stackPanelorder2);

                // 创建单选按钮
                RadioButton radioID = new RadioButton {  Content = "序号", IsChecked = true };
                RadioButton radioName = new RadioButton { Content = "名称" };
                RadioButton radioFile = new RadioButton { Content = "路径"};

                RadioButton radioUp = new RadioButton {  Content = "递增", IsChecked = true };
                RadioButton radioDown = new RadioButton { Content = "递减" };

                RoutedEventHandler Radio_Checked = (s, e) =>
                {
                    if (radioID.IsChecked == true)
                    {
                        CalibrationRsources.SortByID(radioUp.IsChecked== false);
                    }


                    if (radioFile.IsChecked == true)
                    {
                        CalibrationRsources.SortByFilePath(radioUp.IsChecked == false);
                    }
                    orderPopup.IsOpen = false;
                };

                radioID.Checked += Radio_Checked; 
                radioName.Checked += Radio_Checked;
                radioFile.Checked += Radio_Checked;
                radioUp.Checked += Radio_Checked;
                radioDown.Checked += Radio_Checked;


                stackPanelorder1.Children.Add(radioID);
                stackPanelorder1.Children.Add(radioName);
                stackPanelorder1.Children.Add(radioFile);

                stackPanelorder2.Children.Add(radioUp);
                stackPanelorder2.Children.Add(radioDown);


                border.Child = stackPanelorder;
                orderPopup.Child = border;

                StackPanel stack = new StackPanel() { Orientation = Orientation.Horizontal };

                stack.Children.Add(orderButton);


                Button button = new Button() { Content = "上传校正文件", Margin = new Thickness(5) };
                button.Click += (s, e) =>
                {
                    UploadWindow uploadCalibration = new UploadWindow() { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                    uploadCalibration.OnUpload += (s, e) =>
                    {
                        if (s is Upload upload)
                        {
                            if (DService != null)
                            {
                                MsgRecord msgRecord = DService.UploadCalibrationFile(upload.UploadFileName, upload.UploadFilePath, (int)item);
                                msgRecord.MsgRecordStateChanged += (s) =>
                                {

                                };
                            }
                        }
                    };
                    uploadCalibration.ShowDialog();
                };
                stack.Children.Add(button);

                Button button1 = new Button() { Content = "刷新", Margin = new Thickness(5) };
                button1.Click += (s, e) =>
                {
                    CalibrationRsources = CalibrationRsourceService.GetInstance().GetAllCalibrationRsources(item, Device.MySqlId);
                    listView.ItemsSource = CalibrationRsources;
                };
                stack.Children.Add(button1);

                Button button2 = new Button() { Content = "删除", Margin = new Thickness(5) };
                button2.Click += (s, e) =>
                {
                    if (listView.SelectedIndex > -1)
                    {
                        var calibrationRsource = CalibrationRsources[listView.SelectedIndex];
                        CalibrationRsourceService.GetInstance().Delete(calibrationRsource.Id);
                    }
                    else
                    {
                        MessageBox.Show("请选择您要删除的文件");
                    }

                };
                stack.Children.Add(button2);
                stackPanel.Children.Insert(0, stack);
                TabControlCalib.Items.Add(tabItem);
            }
        }




        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ServiceCache_Click(object sender, RoutedEventArgs e)
        {
            DService.CacheClear();
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
                        CalibrationControl calibration = Device.CalibrationParams.Count == 0 ? new CalibrationControl(Device) : new CalibrationControl(Device, Device.CalibrationParams[0].Value);
                        windowTemplate  = new WindowTemplate(TemplateType.Calibration, calibration, Device);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                }
            }
        }

        private void Upload_Calibration_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            CalibrationEdit CalibrationEdit = new CalibrationEdit(Device);
            CalibrationEdit.Show();
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
    }
}
