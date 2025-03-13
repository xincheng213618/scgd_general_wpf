using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Themes.Controls;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayBuildPoi : UserControl
    {
        public AlgorithmBuildPoi IAlgorithm { get; set; }
        public DisplayBuildPoi(AlgorithmBuildPoi iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            ComboxTemplate.ItemsSource = TemplateBuildPoi.Params;
            ComboxTemplate.SelectedIndex = 0;

            CBPOIStorageModel.ItemsSource = EnumExtensions.ToKeyValuePairs<POIStorageModel>();
            CBPOIBuildType.ItemsSource = EnumExtensions.ToKeyValuePairs<POIBuildType>();
            void UpdateCB_SourceImageFiles()
            {
                CB_SourceImageFiles.ItemsSource = ServiceManager.GetInstance().GetImageSourceServices();
                CB_SourceImageFiles.SelectedIndex = 0;
            }
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => UpdateCB_SourceImageFiles();
            UpdateCB_SourceImageFiles();
        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!AlgorithmHelper.IsTemplateSelected(ComboxTemplate, "请先选择BuildPoi模板")) return;

            if (ComboxTemplate.SelectedValue is not ParamBuildPoi param) return;

            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }

                var Params = new Dictionary<string, object>();
                POILayoutTypes POILayoutReq;
                if (CircleChecked.IsChecked  ==true)
                {
                    PointInt center = new PointInt( );
                    center.X = Convert.ToInt32(centerX.Text);
                    center.Y = Convert.ToInt32(centerY.Text);
                    Params.Add("LayoutCenter", center);
                    //CIEParams.Add("LayoutCenterX", centerX.Text);
                    //CIEParams.Add("LayoutCenterY", centerY.Text);
                    Params.Add("LayoutWidth", int.Parse(radius.Text) * 2);
                    Params.Add("LayoutHeight", int.Parse(radius.Text) * 2);
                    POILayoutReq = POILayoutTypes.Circle;
                }
                else if (RectChecked.IsChecked == true)
                {
                    PointInt center = new PointInt();
                    center.X = Convert.ToInt32(rect_centerX.Text);
                    center.Y = Convert.ToInt32(rect_centerY.Text);
                    Params.Add("LayoutCenter", center);
                    //CIEParams.Add("LayoutCenterX", rect_centerX.Text);
                    //CIEParams.Add("LayoutCenterY", rect_centerY.Text);
                    Params.Add("LayoutWidth", width.Text);
                    Params.Add("LayoutHeight", height.Text);
                    POILayoutReq = POILayoutTypes.Rect;
                }
                else//四边形
                {
                    Params.Add("LayoutPolygon", BuildLayoutPolygon());
                    POILayoutReq = POILayoutTypes.PolygonFour;
                }

                MsgRecord msg = IAlgorithm.SendCommand(param, POILayoutReq, Params, code, type, imgFileName, fileExtType, sn);
                ServicesHelper.SendCommand(msg, "关注点布点");
            }
        }

        private List<PointInt> BuildLayoutPolygon()
        {
            List<PointInt> points = new List<PointInt>();
            PointInt point = new PointInt();
            point.X = Convert.ToInt32(Mask_X1.Text);
            point.Y = Convert.ToInt32(Mask_Y1.Text);
            points.Add(point);
            point = new PointInt();
            point.X = Convert.ToInt32(Mask_X2.Text);
            point.Y = Convert.ToInt32(Mask_Y2.Text);
            points.Add(point);
            point = new PointInt();
            point.X = Convert.ToInt32(Mask_X3.Text);
            point.Y = Convert.ToInt32(Mask_Y3.Text);
            points.Add(point);
            point = new PointInt();
            point.X = Convert.ToInt32(Mask_X4.Text);
            point.Y = Convert.ToInt32(Mask_Y4.Text);
            points.Add(point);
            return points;
        }

        private bool GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)
        {
            sn = string.Empty;
            fileExtType = FileExtType.Tif;
            imgFileName = string.Empty;

            bool? isSN = AlgBatchSelect.IsSelected;
            bool? isRaw = AlgRawSelect.IsSelected;

            if (isSN == true)
            {
                if (string.IsNullOrWhiteSpace(AlgBatchCode.Text))
                {
                    MessageBox1.Show(Application.Current.MainWindow, "批次号不能为空，请先输入批次号", "ColorVision");
                    return false;
                }
                sn = AlgBatchCode.Text;
            }
            else if (isRaw == true)
            {
                imgFileName = CB_RawImageFiles.Text;
                fileExtType = FileExtType.Raw;
            }
            else
            {
                imgFileName = ImageFile.Text;
            }
            if (string.IsNullOrWhiteSpace(imgFileName))
            {
                MessageBox1.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
                return false;
            }
            return true;
        }

        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.tiff|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }

        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;
            IAlgorithm.DService.GetRawFiles(deviceService.Code, deviceService.ServiceTypes.ToString());
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                IAlgorithm.DService.Open(deviceService.Code, deviceService.ServiceTypes.ToString(), CB_RawImageFiles.Text, FileExtType.CIE);
        }
    }

    public class PointInt
    {
        public int X { get; set; }
        public int Y { get; set; }

    }
}
