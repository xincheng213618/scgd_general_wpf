﻿#pragma warning disable CS8604,CS8629
using ColorVision.Draw;
using ColorVision.Media;
using ColorVision.Net;
using ColorVision.Services.Dao;
using ColorVision.Common.Utilities;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.Services.Templates;
using ColorVision.Services.Templates.POI;
using ColorVision.Common.Sorts;


namespace ColorVision.Services.Devices.Calibration.Views
{
    /// <summary>
    /// ViewCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ViewCalibration : UserControl, IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ViewCalibration));

        public View View { get; set; }

        public ObservableCollection<ViewResultCalibration> ViewResultCalibrations { get; set; } = new ObservableCollection<ViewResultCalibration>();
        public MQTTCalibration DeviceService{ get; set; }
        public DeviceCalibration Device { get; set; }
        public ViewCalibration(DeviceCalibration device)
        {
            Device = device;
            this.DeviceService = device.DeviceService;
            InitializeComponent();
        }

        public ObservableCollection<TemplateModel<PoiParam>> ComboxPOITemplates { get; set; }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            View = new View();

            listView1.ItemsSource = ViewResultCalibrations;

            ComboxPOITemplates = new ObservableCollection<TemplateModel<PoiParam>>();
            ComboxPOITemplates.Insert(0, new TemplateModel<PoiParam>("Empty", new PoiParam() { Id=-1}));

            foreach (var item in TemplateControl.GetInstance().PoiParams)
                ComboxPOITemplates.Add(item);

            TemplateControl.GetInstance().PoiParams.CollectionChanged += (s, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems != null)
                            foreach (TemplateModel<PoiParam> newItem in e.NewItems)
                                ComboxPOITemplates.Add(newItem);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null)
                            foreach (TemplateModel<PoiParam> newItem in e.OldItems)
                                ComboxPOITemplates.Remove(newItem);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        ComboxPOITemplates.Clear();
                        ComboxPOITemplates.Insert(0, new TemplateModel<PoiParam>("Empty", new PoiParam()) { Id = -1 });
                        break;
                }
            };
            ComboxPOITemplate.ItemsSource = ComboxPOITemplates;
            ComboxPOITemplate.SelectedIndex = 0;

            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
        }


        public void ShowResult(MeasureImgResultModel model)
        {
            ViewResultCalibration result = new ViewResultCalibration(model);
            ViewResultCalibrations.AddUnique(result);

            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0&& listView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
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
            CsvWriter.WriteToCsv(ViewResultCalibrations[listView1.SelectedIndex], dialog.FileName);
            ImageSource bitmapSource = ImageView.ImageShow.Source;
            ImageUtil.SaveImageSourceToFile(bitmapSource, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));

        }



        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResultCalibrations.Clear();
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
            }

        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResultCalibrations.RemoveAt(temp);
            }
        }

        public void OpenImage(CVCIEFile fileData)
        {
            ImageView.OpenImage(fileData);
        }

        MeasureImgResultDao MeasureImgResultDao = new MeasureImgResultDao();

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewResultCalibrations.Clear();
            List<MeasureImgResultModel> algResults = MeasureImgResultDao.GetAll();
            foreach (var item in algResults)
            {
                ViewResultCalibration CameraImgResult = new ViewResultCalibration(item);
                ViewResultCalibrations.AddUnique(CameraImgResult);
            }
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxId.Text) && string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxFile.Text) && string.IsNullOrWhiteSpace(TbDeviceCode.Text) && SearchTimeSart.SelectedDateTime ==DateTime.MinValue)
            {
                ViewResultCalibrations.Clear();
                foreach (var item in MeasureImgResultDao.GetAll())
                {
                    ViewResultCalibration algorithmResult = new ViewResultCalibration(item);
                    ViewResultCalibrations.AddUnique(algorithmResult);
                }
                return;
            }
            else
            {
                ViewResultCalibrations.Clear();
                List<MeasureImgResultModel> algResults = MeasureImgResultDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, TextBoxFile.Text, TbDeviceCode.Text, SearchTimeSart.DisplayDateTime,SearchTimeEnd.DisplayDateTime);
                foreach (var item in algResults)
                {
                    ViewResultCalibration algorithmResult = new ViewResultCalibration(item);
                    ViewResultCalibrations.AddUnique(algorithmResult);
                }

            }
        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SearchTimeSart.SelectedDateTime = DateTime.MinValue;
            SearchTimeEnd.SelectedDateTime = DateTime.Now;

            SerchPopup.IsOpen = true;
            TextBoxId.Text = string.Empty;
            TextBoxBatch.Text = string.Empty;
            TextBoxFile.Text = string.Empty;
            TbDeviceCode.Text = string.Empty;
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ViewResultCalibration viewResult)
            {
                ViewResultCalibrations.Remove(viewResult);
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
                ViewResultCalibrations.SortByID(RadioUp?.IsChecked == false);
            }

            if (RadioBatch?.IsChecked == true)
            {
                ViewResultCalibrations.SortByBatch(RadioUp?.IsChecked == false);
            }

            if (RadioFilePath?.IsChecked == true)
            {
                ViewResultCalibrations.SortByFilePath(RadioUp?.IsChecked == false);
            }

            if (RadioCreateTime?.IsChecked == true)
            {
                ViewResultCalibrations.SortByCreateTime(RadioUp?.IsChecked == false);
            }

            OrderPopup.IsOpen = false;
        }

        private void Src_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex == -1) return;
            var ViewResultCamera = ViewResultCalibrations[listView1.SelectedIndex];
        }

        private void X_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex == -1) return;
            var ViewResultCamera = ViewResultCalibrations[listView1.SelectedIndex];
        }

        private void Z_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex == -1) return;
            var ViewResultCamera = ViewResultCalibrations[listView1.SelectedIndex];
        }
        private void Y_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex == -1) return;
            var ViewResultCamera = ViewResultCalibrations[listView1.SelectedIndex];

        }
        private void B_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex == -1) return;
            var ViewResultCamera = ViewResultCalibrations[listView1.SelectedIndex];
        }

        private void R_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex == -1) return;
            var ViewResultCamera = ViewResultCalibrations[listView1.SelectedIndex];
        }

        private void G_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex == -1) return;
            var ViewResultCamera = ViewResultCalibrations[listView1.SelectedIndex];
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content !=null)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        switch (item.ColumnName)
                        {
                            case "序号":
                                item.IsSortD = !item.IsSortD;
                                ViewResultCalibrations.SortByID(item.IsSortD);
                                break;
                            case "测量时间":
                                item.IsSortD = !item.IsSortD;
                                ViewResultCalibrations.SortByCreateTime(item.IsSortD);
                                break;
                            case "批次号":
                                item.IsSortD = !item.IsSortD;
                                ViewResultCalibrations.SortByBatch(item.IsSortD);
                                break;
                            case "图像数据文件":
                                item.IsSortD = !item.IsSortD;
                                ViewResultCalibrations.SortByFilePath(item.IsSortD);
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void POI_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxPOITemplate.SelectedValue is PoiParam poiParams)
            {
                if (poiParams.Id == -1)
                {
                    ImageView.ImageShow.Clear();
                    return;
                }
                ImageView.ImageShow.Clear();
                TemplateControl.GetInstance().LoadPoiDetailFromDB(poiParams);
                foreach (var item in poiParams.PoiPoints)
                {
                    switch (item.PointType)
                    {
                        case RiPointTypes.Circle:
                            DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixWidth;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.ID = item.ID;
                            Circle.Attribute.Text = item.Name;
                            Circle.Render();
                            ImageView.ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                            Rectangle.Attribute.Rect = new Rect(item.PixX, item.PixY, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.ID = item.ID;
                            Rectangle.Attribute.Name = item.Name;
                            Rectangle.Render();
                            ImageView.ImageShow.AddVisual(Rectangle);
                            break;
                        case RiPointTypes.Mask:
                            break;
                    }
               }
            }
        }
    }
}
