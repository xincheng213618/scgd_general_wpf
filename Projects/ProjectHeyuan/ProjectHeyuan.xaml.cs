#pragma warning disable CS8602
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.DAO;
using ColorVision.Services.Devices.Algorithm.Dao;
using ColorVision.Services.Devices.Algorithm.Views;
using ColorVision.Services.Flow;
using ColorVision.Services.Templates.POI.Validate;
using ColorVision.Solution;
using FlowEngineLib;
using NPOI.SS.Formula.Functions;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Projects
{

    public sealed class ConnectConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isconnect)
            {
                return isconnect ? "已经连接":"未连接";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting from a string to a memory size is not supported.");
        }
    }


    public sealed class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isconnect)
            {
                return isconnect ? Brushes.Red : Brushes.Blue;
            }
            return Brushes.Black; ;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Converting from a string to a memory size is not supported.");
        }
    }

    /// <summary>
    /// ProjectHeyuanWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectHeyuanWindow : Window
    {
        public ProjectHeyuanWindow()
        {
            InitializeComponent();
        }

        public ObservableCollection<TempResult> Settings { get; set; } = new ObservableCollection<TempResult>();
        public ObservableCollection<TempResult> Results { get; set; } = new ObservableCollection<TempResult>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            ListViewSetting.ItemsSource = Settings;
            Results.Add(new TempResult() { Name = "White" });
            Results.Add(new TempResult() { Name = "Blue" });
            Results.Add(new TempResult() { Name = "Orange" });
            Results.Add(new TempResult() { Name = "Red" });
            ListViewResult.ItemsSource = Results;

            ComboBoxSer.ItemsSource = SerialPort.GetPortNames();
            ComboBoxSer.SelectedIndex = 0;

            ListViewMes.ItemsSource = HYMesManager.GetInstance().SerialMsgs;
            FlowTemplate.ItemsSource = FlowParam.Params;
            ValidateTemplate.ItemsSource = ValidateParam.CIEParams;
            this.DataContext = HYMesManager.GetInstance();
        }
        private Services.Flow.FlowControl flowControl;

        private IPendingHandler handler;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            if (sender != null)
            {
                FlowControlData FlowControlData = (FlowControlData)sender;
 
                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    if (FlowControlData.EventName == "Completed")
                    {
                        ResultText.Text = "OK";
                        ResultText.Foreground = Brushes.Blue;
                        Results.Clear();
                        var Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
                        if (Batch != null)
                        {
                            List<POIPointResultModel> POIPointResultModels = POIPointResultDao.Instance.GetAllByPid(Batch.Id);
                            List<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                            foreach (var item in POIPointResultModels)
                            {
                                PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(item);
                                PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                            }
                            for (int i = 0; i < PoiResultCIExyuvDatas[0].ValidateSingles.Count; i++)
                            {
                                //Results.Add(new TempResult() { Name = PoiResultCIExyuvDatas[0].ValidateSingles[i].Rule.RType.ToString(), NumSet = new NumSet() { Orange = PoiResultCIExyuvDatas[0].ValidateSingles[i].Result.ToString() } });
                            }
                        }
                        HYMesManager.GetInstance().UploadMes();
                    }
                    else
                    {
                        ResultText.Text =  "不合格";
                        ResultText.Foreground =  Brushes.Red;
                        HYMesManager.GetInstance().UploadMes();
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(HYMesManager.GetInstance().SN))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "产品编号为空，在运行前请配置产品编号");
                return;
            }

            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = FlowDisplayControl.GetInstance().View.FlowEngineControl.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl = new Services.Flow.FlowControl(MQTTControl.GetInstance(), FlowDisplayControl.GetInstance().View.FlowEngineControl);

                    handler = PendingBox.Show(Application.Current.MainWindow, "TTL:" + "0", "流程运行", true);

                    flowControl.FlowData += (s, e) =>
                    {
                        if (s is FlowControlData msg)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                handler?.UpdateMessage("TTL: " + msg.Params.TTL.ToString());
                            });
                        }
                    };
                    flowControl.FlowCompleted += FlowControl_FlowCompleted;
                    string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                    flowControl.Start(sn);
                    string name = string.Empty;
                    BeginNewBatch(sn, name);
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到完整流程，运行失败", "ColorVision");
                }
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "流程为空，请选择流程运行", "ColorVision");
            }
        }

        public static void BeginNewBatch(string sn, string name)
        {
            BatchResultMasterModel batch = new();
            batch.Name = string.IsNullOrEmpty(name) ? sn : name;
            batch.Code = sn;
            batch.CreateDate = DateTime.Now;
            batch.TenantId = 0;
            BatchResultMasterDao.Instance.Save(batch);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (!HYMesManager.GetInstance().IsConnect)
            {
                int i = HYMesManager.GetInstance().OpenPort(ComboBoxSer.Text);
            }
            else
            {
                HYMesManager.GetInstance().Close();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().UploadSN();
        }

        private void SelectDataPath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                HYMesManager.Config.DataPath = dialog.SelectedPath;
            }
        }

        private void UploadSN(object sender, RoutedEventArgs e)
        {
            HYMesManager.GetInstance().UploadSN();
        }

        private void ValidateTemplate_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ValidateTemplate.SelectedValue is ValidateParam validateParam)
            {
                Settings.Clear();
                TempResult tempResult = new TempResult() { Name = "White"};

                foreach (var item in validateParam.ValidateSingles)
                {
                    if (item.Model.Code == "CIE_x")
                    {
                        tempResult.X = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_y")
                    {
                        tempResult.Y = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_lv")
                    {
                        tempResult.Lv = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                }

                TempResult tempResult1 = new TempResult() { Name = "Blue" };

                foreach (var item in validateParam.ValidateSingles)
                {
                    if (item.Model.Code == "CIE_x")
                    {
                        tempResult1.X = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_y")
                    {
                        tempResult1.Y = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_lv")
                    {
                        tempResult1.Lv = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                }

                TempResult tempResult2 = new TempResult() { Name = "Red" };

                foreach (var item in validateParam.ValidateSingles)
                {
                    if (item.Model.Code == "CIE_x")
                    {
                        tempResult2.X = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_y")
                    {
                        tempResult2.Y = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_lv")
                    {
                        tempResult2.Lv = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                }

                TempResult tempResult3 = new TempResult() { Name = "Orange" };

                foreach (var item in validateParam.ValidateSingles)
                {
                    if (item.Model.Code == "CIE_x")
                    {
                        tempResult3.X = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_y")
                    {
                        tempResult3.Y = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                    if (item.Model.Code == "CIE_lv")
                    {
                        tempResult3.Lv = new NumSet() { ValMin = item.ValMin, ValMax = item.ValMax };
                    }
                }



                Settings.Add(tempResult);
                Settings.Add(tempResult1);
                Settings.Add(tempResult2);
                Settings.Add(tempResult3);

            }
        }
    }
}
