#pragma warning disable CS8604,CS8629
using ColorVision.Device.Camera;
using ColorVision.Media;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Net;
using ColorVision.Services.Algorithm;
using ColorVision.Services.Device.Camera.Views;
using ColorVision.Sorts;
using ColorVision.Templates;
using ColorVision.Util;
using log4net;
using MQTTMessageLib.Camera;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Services.Device.Camera.Views
{
    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCamera : UserControl, IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ViewCamera));
        public View View { get; set; }

        public event ImgCurSelectionChanged OnCurSelectionChanged;
        public ObservableCollection<ViewResultCamera> ViewResultCameras { get; set; } = new ObservableCollection<ViewResultCamera>();
        public DeviceServiceCamera DService{ get; set; }
        public ViewCamera(DeviceServiceCamera ds)
        {
            this.DService = ds;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            View= new View();
            listView1.ItemsSource = ViewResultCameras;
        }

        private void Button_Click_ShowResultGrid(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                Visibility visibility = button.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                listView1.Visibility = visibility;
            }
        }

        private void Button_Click_Export(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show(Application.Current.MainWindow, "您需要先选择数据", "ColorVision");
                return;
            }
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            CsvWriter.WriteToCsv(ViewResultCameras[listView1.SelectedIndex], dialog.FileName);
            ImageSource bitmapSource = ImageView.ImageShow.Source;
            ImageUtil.SaveImageSourceToFile(bitmapSource, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));

        }



        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResultCameras.Clear();
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
                OnCurSelectionChanged?.Invoke(ViewResultCameras[listView1.SelectedIndex]);
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResultCameras.RemoveAt(temp);
            }
        }

        public void OpenImage(byte[] bytes)
        {
            ImageView.OpenImage(bytes);
        }
        public void OpenImage(CVCIEFileInfo fileData)
        {
            ImageView.OpenImage(fileData);
        }

        public void ShowResult(MeasureImgResultModel model)
        {
            ViewResultCamera result = new ViewResultCamera(model);
            ViewResultCameras.Add(result);

            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }


        MeasureImgResultDao MeasureImgResultDao = new MeasureImgResultDao();

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResultCameras.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.GetAll();
            foreach (var item in algResults)
            {
                ViewResultCamera CameraImgResult = new ViewResultCamera(item);
                ViewResultCameras.Add(CameraImgResult);
            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxId.Text) && string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxFile.Text) && string.IsNullOrWhiteSpace(TbDeviceCode.Text))
            {
                ViewResultCameras.Clear();
                foreach (var item in MeasureImgResultDao.GetAll())
                {
                    ViewResultCamera algorithmResult = new ViewResultCamera(item);
                    ViewResultCameras.Add(algorithmResult);
                }
                return;
            }
            else
            {
                ViewResultCameras.Clear();
                List<MeasureImgResultModel> algResults = MeasureImgResultDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, TextBoxFile.Text, TbDeviceCode.Text);
                foreach (var item in algResults)
                {
                    ViewResultCamera algorithmResult = new ViewResultCamera(item);
                    ViewResultCameras.Add(algorithmResult);
                }

            }
        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SerchPopup.IsOpen = true;
            TextBoxId.Text = string.Empty;
            TextBoxBatch.Text = string.Empty;
            TextBoxFile.Text = string.Empty;
            TbDeviceCode.Text = string.Empty;
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCamera viewResult)
            {
                ViewResultCameras.Remove(viewResult);
                ImageView.Clear();
            }
        }

        private void Order_Click(object sender, RoutedEventArgs e)
        {
            OrderPopup.IsOpen = true;
        }

        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (RadioID?.IsChecked == true)
            {
                ViewResultCameras.SortByID(RadioUp?.IsChecked == false);
            }

            if (RadioBatch?.IsChecked == true)
            {
                ViewResultCameras.SortByBatch(RadioUp?.IsChecked == false);
            }

            if (RadioFilePath?.IsChecked == true)
            {
                ViewResultCameras.SortByFilePath(RadioUp?.IsChecked == false);
            }

            if (RadioCreateTime?.IsChecked == true)
            {
                ViewResultCameras.SortByCreateTime(RadioUp?.IsChecked == false);
            }

            OrderPopup.IsOpen = false;
        }

        private void Src_Click(object sender, RoutedEventArgs e)
        {
            var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
            var msgRecord = DService.GetChannel(ViewResultCamera.ID, CVImageChannelType.SRC);
        }

        private void X_Click(object sender, RoutedEventArgs e)
        {
            var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
            var msgRecord = DService.GetChannel(ViewResultCamera.ID, CVImageChannelType.CIE_XYZ_X);
        }

        private void Z_Click(object sender, RoutedEventArgs e)
        {
            var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
            var msgRecord = DService.GetChannel(ViewResultCamera.ID, CVImageChannelType.CIE_XYZ_Z);
        }
        private void Y_Click(object sender, RoutedEventArgs e)
        {
            var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
            var msgRecord = DService.GetChannel(ViewResultCamera.ID, CVImageChannelType.CIE_XYZ_Y);

        }
        private void B_Click(object sender, RoutedEventArgs e)
        {
            var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
            var msgRecord = DService.GetChannel(ViewResultCamera.ID, CVImageChannelType.RGB_B);
        }

        private void R_Click(object sender, RoutedEventArgs e)
        {
            var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
            var msgRecord = DService.GetChannel(ViewResultCamera.ID, CVImageChannelType.RGB_R);
        }

        private void G_Click(object sender, RoutedEventArgs e)
        {
            var ViewResultCamera = ViewResultCameras[listView1.SelectedIndex];
            var msgRecord = DService.GetChannel(ViewResultCamera.ID, CVImageChannelType.RGB_G);
        }
    }
}
