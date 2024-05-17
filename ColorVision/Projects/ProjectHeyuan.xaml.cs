using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.Dao;
using ColorVision.Services.DAO;
using ColorVision.Services.Devices.Algorithm.Dao;
using ColorVision.Services.Devices.Algorithm.Views;
using ColorVision.Services.Flow;
using ColorVision.UI;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Projects
{

    public class ProjectHeyuanExport : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "HeYuan";

        public int Order => 100;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "河源精电";
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
            new ProjectHeyuan() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    /// <summary>
    /// ProjectHeyuan.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectHeyuan : Window
    {
        public ProjectHeyuan()
        {
            InitializeComponent();
        }

        public ObservableCollection<TempResult> Settings { get; set; } = new ObservableCollection<TempResult>();
        public ObservableCollection<TempResult> Results { get; set; } = new ObservableCollection<TempResult>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            Settings.Add(new TempResult() { Name = "x(上限)"});
            Settings.Add(new TempResult() { Name = "x(下限)" });
            Settings.Add(new TempResult() { Name = "y(上限)" });
            Settings.Add(new TempResult() { Name = "y(下限)" });
            Settings.Add(new TempResult() { Name = "lv(上限)" });
            Settings.Add(new TempResult() { Name = "lv(下限)" });
            ListViewSetting.ItemsSource = Settings;
            Results.Add(new TempResult() { Name = "x" });
            Results.Add(new TempResult() { Name = "y" });
            Results.Add(new TempResult() { Name = "lv" });
            ListViewResult.ItemsSource = Results;

            ComboBoxSer.ItemsSource = SerialPort.GetPortNames();
            ComboBoxSer.SelectedIndex = 0;

            ListViewMes.ItemsSource = HYMesManager.GetInstance().SerialMsgs;

            FlowTemplate.ItemsSource = FlowParam.Params;
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
                        HYMesManager.GetInstance().SendSn("0", FlowControlData.SerialNumber);

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

                                Results.Add(new TempResult() { Name = PoiResultCIExyuvDatas[0].ValidateSingles[i].Rule.RType.ToString(), NumSet = new NumSet() { Orange = PoiResultCIExyuvDatas[0].ValidateSingles[i].Result.ToString() } });
                            }


                        }
                    }
                    else
                    {
                        ResultText.Text =  "不合格";
                        ResultText.Foreground =  Brushes.Red;
                        HYMesManager.GetInstance().SendSn("0", "2222");
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
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
                    ServiceManager.BeginNewBatch(sn, name);
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到完整流程，运行失败", "ColorVision");
                }
            }



        }


    }
}
