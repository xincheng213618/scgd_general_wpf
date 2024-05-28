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
using CVCommCore;
using FlowEngineLib;
using log4net;
using Microsoft.DwayneNeed.Win32.User32;
using Microsoft.VisualBasic.Logging;
using Mysqlx.Crud;
using NPOI.SS.Formula.Functions;
using Panuon.WPF.UI;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
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
                return isconnect ? Brushes.Blue : Brushes.Red;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectHeyuanWindow));
        public ProjectHeyuanWindow()
        {
            InitializeComponent();
        }

        public ObservableCollection<TempResult> Settings { get; set; } = new ObservableCollection<TempResult>();
        public ObservableCollection<TempResult> Results { get; set; } = new ObservableCollection<TempResult>();


        private FlowEngineLib.FlowEngineControl flowEngine;
        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);

            STNodeEditor STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            ListViewSetting.ItemsSource = Settings;
            ListViewResult.ItemsSource = Results;
            ComboBoxSer.ItemsSource = SerialPort.GetPortNames();
            ComboBoxSer.SelectedIndex = 0;

            ListViewMes.ItemsSource = HYMesManager.GetInstance().SerialMsgs;
            FlowTemplate.ItemsSource = FlowParam.Params;
            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedIndex > -1)
                {
                    var tokens = ServiceManager.GetInstance().ServiceTokens;
                    flowEngine.LoadFromBase64(FlowParam.Params[FlowTemplate.SelectedIndex].Value.DataBase64, tokens);
                }
            };

            List<string> strings = new List<string>() { "White", "Blue", "Red", "Orange" };
            foreach (var item in strings)
            {
                Settings.Add(new TempResult() { Name = item });
            }

            this.DataContext = HYMesManager.GetInstance();
        }
        private Services.Flow.FlowControl flowControl;

        private IPendingHandler handler;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            if (sender is FlowControlData FlowControlData)
            {
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
                            var resultMaster = AlgResultMasterDao.Instance.GetAllByBatchid(Batch.Id);
                            List<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new List<PoiResultCIExyuvData>();
                            foreach (var item in resultMaster)
                            {
                                List<POIPointResultModel> POIPointResultModels = POIPointResultDao.Instance.GetAllByPid(item.Id);

                                foreach (var pointResultModel in POIPointResultModels)
                                {
                                    PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData(pointResultModel);
                                    PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                                }
                            }

                            Results.Clear();
                            if (PoiResultCIExyuvDatas.Count ==4)
                            {
                                for (int i = 0; i < PoiResultCIExyuvDatas.Count; i++)
                                {
                                    List<string> strings = new List<string>() { "White", "Blue", "Red", "Orange" };
                                    var poiResultCIExyuvData1 = PoiResultCIExyuvDatas[i];
                                    TempResult tempResult1 = new TempResult() { Name = poiResultCIExyuvData1.Name };
                                    tempResult1.X = new NumSet() { Value = (float)poiResultCIExyuvData1.x };
                                    tempResult1.Y = new NumSet() { Value = (float)poiResultCIExyuvData1.y };
                                    tempResult1.Lv = new NumSet() { Value = (float)poiResultCIExyuvData1.Y };
                                    foreach (var item in poiResultCIExyuvData1.ValidateSingles)
                                    {
                                        if (item.Rule.RType == ValidateRuleType.CIE_x)
                                        {
                                        }
                                        if (item.Rule.RType == ValidateRuleType.CIE_y)
                                        {
                                        }
                                        if (item.Rule.RType == ValidateRuleType.CIE_Y)
                                        {
                                        }
                                    }
                                    Results.Add(tempResult1);

                                    var sortedResults = Results.OrderBy(r => strings.IndexOf(r.Name)).ToList();
                                    Results.Clear();
                                    foreach (var result in sortedResults)
                                    {
                                        Results.Add(result);
                                    }

                                }
                                HYMesManager.GetInstance().UploadMes(Results);
                                log.Debug("mes 已经上传");
                            }
                            else
                            {
                                MessageBox.Show(Application.Current.GetActiveWindow(), "流程结果数据错误", "ColorVision");
                            }
                        }
                        else
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号", "ColorVision");
                        }
                    }
                    else
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName, "ColorVision");
                    }
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "流程运行失败" + FlowControlData.EventName, "ColorVision");
                }

            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "1", "ColorVision");
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
                string startNode = flowEngine.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl ??= new Services.Flow.FlowControl(MQTTControl.GetInstance(), flowEngine);

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
            if (sender is ComboBox comboBox && comboBox.Tag is TempResult tempResult && comboBox.SelectedValue is ValidateParam validateParam)
            {
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
            }
        }

        private void ValidateTemplate_Initialized(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.ItemsSource = ValidateParam.CIEParams;
            }
        }
    }
}
