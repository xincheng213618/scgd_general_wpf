#pragma warning disable CS8604,CS8629
using ColorVision.Media;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Services.Algorithm;
using ColorVision.Services.Device.Camera.Views;
using ColorVision.Sort;
using ColorVision.Templates;
using ColorVision.Util;
using FileServerPlugin;
using log4net;
using MQTTMessageLib.Algorithm;
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
    /// CameraView.xaml 的交互逻辑
    /// </summary>
    public partial class CameraView : UserControl, IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(CameraView));
        public View View { get; set; }

        public event ImgCurSelectionChanged OnCurSelectionChanged;
        public ObservableCollection<CameraViewResult> ViewResults { get; set; } = new ObservableCollection<CameraViewResult>();
        public CameraView()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            View= new View();
            listView1.ItemsSource = ViewResults;
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
            CsvWriter.WriteToCsv(ViewResults[listView1.SelectedIndex], dialog.FileName);
            ImageSource bitmapSource = ImageView.ImageShow.Source;
            ImageUtil.SaveImageSourceToFile(bitmapSource, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));

        }



        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
                OnCurSelectionChanged?.Invoke(ViewResults[listView1.SelectedIndex]);
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResults.RemoveAt(temp);
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
            CameraViewResult result = new CameraViewResult(model);
            ViewResults.Add(result);

            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }


        MeasureImgResultDao MeasureImgResultDao = new MeasureImgResultDao();

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.GetAll();
            foreach (var item in algResults)
            {
                CameraViewResult CameraImgResult = new CameraViewResult(item);
                ViewResults.Add(CameraImgResult);
            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxId.Text) && string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxFile.Text) && string.IsNullOrWhiteSpace(TbDeviceCode.Text))
            {
                ViewResults.Clear();
                foreach (var item in MeasureImgResultDao.GetAll())
                {
                    CameraViewResult algorithmResult = new CameraViewResult(item);
                    ViewResults.Add(algorithmResult);
                }
                return;
            }
            else
            {
                ViewResults.Clear();
                List<MeasureImgResultModel> algResults = MeasureImgResultDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, TextBoxFile.Text, TbDeviceCode.Text);
                foreach (var item in algResults)
                {
                    CameraViewResult algorithmResult = new CameraViewResult(item);
                    ViewResults.Add(algorithmResult);
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
            if (sender is MenuItem menuItem && menuItem.Tag is CameraViewResult viewResult)
            {
                ViewResults.Remove(viewResult);
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
                ViewResults.SortById(RadioUp?.IsChecked == true);
            }

            if (RadioBatch?.IsChecked == true)
            {
                ViewResults.SortByBatch(RadioUp?.IsChecked == true);
            }

            if (RadioFilePath?.IsChecked == true)
            {
                ViewResults.SortByFilePath(RadioUp?.IsChecked == true);
            }

            if (RadioCreateTime?.IsChecked == true)
            {
                ViewResults.SortByCreateTime(RadioUp?.IsChecked == true);
            }

            OrderPopup.IsOpen = false;
        }
    }


}
