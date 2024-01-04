using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using ColorVision.Extension;
using ColorVision.Services.Msg;
using ColorVision.Sort;
using ColorVision.Templates;
using ColorVision.Themes.Controls;
using cvColorVision;
using SkiaSharp;

namespace ColorVision.Device.Camera
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCameraControl : UserControl
    {
        public DeviceCamera DeviceCamera { get; set; }

        public DeviceServiceCamera DService { get => DeviceCamera.DeviceService; }

        public bool IsCanEdit { get; set; }
        public DeviceCameraControl(DeviceCamera mQTTDeviceCamera,bool isCanEdit =true)
        {
            DeviceCamera = mQTTDeviceCamera;
            IsCanEdit = isCanEdit;
            InitializeComponent();
            this.Loaded += DeviceCameraControl_Loaded;
        }

        private void DeviceCameraControl_Loaded(object sender, RoutedEventArgs e)
        {
            DeviceCamera.IsEditMode = false;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (!IsCanEdit) ButtonEdit.Visibility = IsCanEdit ? Visibility.Visible : Visibility.Collapsed;
            this.DataContext = DeviceCamera;
            if (IsCanEdit)
            {
                UserControl userControl = DeviceCamera.GetEditControl();
                if (userControl.Parent is Panel grid)
                    grid.Children.Remove(userControl);
                MQTTEditContent.Children.Add(userControl);
            }


            foreach (var item in Enum.GetValues(typeof(ResouceType)).Cast<ResouceType>())
            {
                TabItem tabItem = new TabItem();
                tabItem.Header = item.ToDescription();

                StackPanel stackPanel = new StackPanel();
                tabItem.Content = stackPanel;

                ListView listView = new ListView();

                GridView gridView = new GridView();
                gridView.Columns.Add(new GridViewColumn() { Header = "序号", DisplayMemberBinding = new Binding("ID") });
                gridView.Columns.Add(new GridViewColumn() { Header = "名称", DisplayMemberBinding = new Binding("Name") });
                gridView.Columns.Add(new GridViewColumn() { Header = "路径", DisplayMemberBinding = new Binding("FilePath") });
                listView.View = gridView;
                stackPanel.Children.Add(listView);


                ObservableCollection<CalibrationRsource> CalibrationRsources = CalibrationRsourceService.GetInstance().GetAllCalibrationRsources(item, DeviceCamera.MySqlId);

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
                        CalibrationRsources.SortById(radioUp.IsChecked==true);
                    }

                    if (radioName.IsChecked == true)
                    {
                        CalibrationRsources.SortByName(radioUp.IsChecked == true);
                    }

                    if (radioFile.IsChecked == true)
                    {
                        CalibrationRsources.SortByFilePath(radioUp.IsChecked == true);
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
                    CalibrationUploadWindow uploadCalibration = new CalibrationUploadWindow(DService, item) { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                    uploadCalibration.OnUpload += (s, e) =>
                    {
                        if (s is Upload upload)
                        {
                            MsgRecord msgRecord = DService?.UploadCalibrationFile(upload.UploadFileName, upload.UploadFilePath, (int)item);
                            msgRecord.MsgRecordStateChanged += (s) =>
                            {
                                CalibrationRsources = CalibrationRsourceService.GetInstance().GetAllCalibrationRsources(item, DeviceCamera.MySqlId);
                                listView.ItemsSource = CalibrationRsources;
                            };
                        }
                    };
                    uploadCalibration.ShowDialog();
                };
                stack.Children.Add(button);

                Button button1 = new Button() { Content = "刷新", Margin = new Thickness(5) };
                button1.Click += (s, e) =>
                {
                    CalibrationRsources = CalibrationRsourceService.GetInstance().GetAllCalibrationRsources(item, DeviceCamera.MySqlId);
                    listView.ItemsSource = CalibrationRsources;
                };
                stack.Children.Add(button1);

                Button button2 = new Button() { Content = "删除", Margin = new Thickness(5) };
                button2.Click += (s, e) =>
                {
                    if (listView.SelectedIndex > -1)
                    {
                        CalibrationRsource calibrationRsource = CalibrationRsources[listView.SelectedIndex];
                        CalibrationRsourceService.GetInstance().Delete(calibrationRsource.ID);
                        CalibrationRsources = CalibrationRsourceService.GetInstance().GetAllCalibrationRsources(item, DeviceCamera.MySqlId);
                        listView.ItemsSource = CalibrationRsources;
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
    }
}
