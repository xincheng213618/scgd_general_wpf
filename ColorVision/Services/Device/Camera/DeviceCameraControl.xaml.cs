using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Extension;
using ColorVision.Services.Msg;
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

                StackPanel StackPanelSort = new StackPanel() { Orientation = Orientation.Horizontal };



                ObservableCollection<CalibrationRsource> CalibrationRsources = CalibrationRsourceService.GetInstance().GetAllCalibrationRsources(item, DeviceCamera.MySqlId);

                listView.ItemsSource = CalibrationRsources;


                StackPanelSort.Children.Clear();
                stackPanel.Children.Insert(0, StackPanelSort);


                RadioButton IDDESC = new RadioButton { Content = "按照序号升序", Tag = "IDDESC", Margin = new Thickness(5) ,IsChecked =true};
                IDDESC.Click += (s, e) =>
                {
                    var sortedItems = CalibrationRsources.OrderBy(f => f.ID).ToList();
                    CalibrationRsources.Clear();
                    foreach (var item in sortedItems)
                    {
                        CalibrationRsources.Add(item);
                    }
                };
                StackPanelSort.Children.Add(IDDESC);

                RadioButton IDASC = new RadioButton { Content = "按照序号降序", Tag = "IDASC", Margin = new Thickness(5) };
                IDASC.Click += (s, e) =>
                {
                    var sortedItems = CalibrationRsources.OrderByDescending(f => f.ID).ToList();
                    CalibrationRsources.Clear();
                    foreach (var item in sortedItems)
                    {
                        CalibrationRsources.Add(item);
                    }
                };
                StackPanelSort.Children.Add(IDASC);

                RadioButton BatchASC = new RadioButton { Content = "按照批次号升序", Tag = "BatchASC", Margin = new Thickness(5) };
                BatchASC.Click += (s, e) =>
                {
                    var sortedItems = CalibrationRsources.OrderBy(f => f.Name).ToList();
                    CalibrationRsources.Clear();
                    foreach (var item in sortedItems)
                    {
                        CalibrationRsources.Add(item);
                    }
                };
                StackPanelSort.Children.Add(BatchASC);

                RadioButton BatchESC = new RadioButton { Content = "按照批次号降序", Tag = "BatchESC", Margin = new Thickness(5) };
                BatchESC.Click += (s, e) =>
                {
                    var sortedItems = CalibrationRsources.OrderByDescending(f => f.Name).ToList();
                    CalibrationRsources.Clear();
                    foreach (var item in sortedItems)
                    {
                        CalibrationRsources.Add(item);
                    }
                };
                StackPanelSort.Children.Add(BatchESC);

                StackPanel stack = new StackPanel() {  Orientation =Orientation.Horizontal};
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
