using ColorVision.MQTT;
using ColorVision.Template;
using FlowEngineLib;
using FlowEngineLib.Start;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FlowEngine
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class WindowFlowEngine : Window
    {
        public bool IsSave { get; set; } = true;

        HslCommunication.BasicFramework.SoftNumericalOrder softNumerical;
        public WindowFlowEngine()
        {
            InitializeComponent();
            ButtonSave.Visibility = Visibility.Collapsed;
            ButtonClear.Visibility = Visibility.Collapsed;
        }
        public WindowFlowEngine(string FileName)
        {
            InitializeComponent();
            if (File.Exists(FileName))
            {
                OpenFlow(FileName);
                ButtonOpen.Visibility = Visibility.Collapsed;
                ButtonNew.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.FileName = FileName;
                ButtonOpen.Visibility = Visibility.Collapsed;
                ButtonNew.Visibility = Visibility.Collapsed;
                IsSave = false;
            }
        }
        FlowParam FlowParam { get; set; }
        public WindowFlowEngine(FlowParam flowParam)
        {
            FlowParam = flowParam;
            InitializeComponent();


            if (File.Exists(flowParam.FileName))
            {
                OpenFlow(flowParam.FileName);
                ButtonOpen.Visibility = Visibility.Collapsed;
                ButtonNew.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.FileName = flowParam.Name;
                ButtonOpen.Visibility = Visibility.Collapsed;
                ButtonNew.Visibility = Visibility.Collapsed;
                IsSave = false;
            }
        }




        private void Window_Initialized(object sender, EventArgs e)
        {
            STNodePropertyGrid1.Text = "属性";
            STNodeTreeView1.LoadAssembly("FlowEngineLib.dll");
            STNodeEditor1.LoadAssembly("FlowEngineLib.dll");
            STNodeEditor1.ActiveChanged += (s, e) => STNodePropertyGrid1.SetNode(STNodeEditor1.ActiveNode);
            STNodeEditor1.NodeAdded += StNodeEditor1_NodeAdded;
            ;
            STNodeEditor1.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Delete)
                {
                    if (STNodeEditor1.ActiveNode !=null)
                        STNodeEditor1.Nodes.Remove(STNodeEditor1.ActiveNode);
                    foreach (var item in STNodeEditor1.GetSelectedNode())
                    {
                        STNodeEditor1.Nodes.Remove(item);
                    }
                }
            };
            softNumerical = new HslCommunication.BasicFramework.SoftNumericalOrder("CV", "yyyyMMddHH", 5, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\softNumerical.txt");
            TextBoxsn.Text = softNumerical.GetNumericalOrder();

            string iPStr = "192.168.3.225";
            int port = 1883;
            string uName = "";
            string uPwd = "";

            FlowEngineLib.MQTTHelper.SetDefaultCfg(iPStr, port, uName, uPwd, false, null);

            this.Closed +=(s,e)=>
            {
                if (IsSave)
                {
                    //SaveFlow(FileName);
                }
                else
                {
                    if (MessageBox.Show("您是否保存", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                        ofd.Filter = "*.stn|*.stn";
                        ofd.FileName = FileName;
                        if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        if (FlowParam != null)
                        {
                            FlowParam.FileName = ofd.FileName;
                        }
                        SaveFlow(ofd.FileName, true);
                    }
                    else
                    {
                        
                    }
                }
            };
        }

        private string startNodeName;
        private void StNodeEditor1_NodeAdded(object sender, STNodeEditorEventArgs e)
        {
            STNode node = e.Node;
            if (e.Node != null && e.Node is BaseStartNode nodeStart)
            {
                startNodeName = nodeStart.NodeName;
            }
            node.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditor1.Nodes.Remove(node));
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            SaveFlow(FileName);
            STNodeEditor1.Nodes.Clear();
        }

        private void Button_Click_Save(object sender, RoutedEventArgs e)
        {
            if (File.Exists(FileName))
            {
                IsSave = true;
                SaveFlow(FileName);
            }
            else if (!IsSave)
            {


                System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                ofd.Filter = "*.stn|*.stn";
                ofd.FileName = FileName;
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                if (FlowParam != null)
                {
                    FlowParam.FileName = ofd.FileName;
                }
                SaveFlow(ofd.FileName,true);
                IsSave = true;
            }
            else
            {
                MessageBox.Show("请先创建流程");
            }
        }
        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("您是否清空已经创建流程\n\r清空后自动保存关闭", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                STNodeEditor1.Nodes.Clear();
        }

        string FileName { get; set; }

        private string svrName;

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            ButtonSave.Visibility = Visibility.Visible;
            OpenFlow(ofd.FileName);
        }
        FlowControl flowControl;

        private void OpenFlow(string flowName)
        {
            FileName = flowName;
            STNodeEditor1.Nodes.Clear();
            STNodeEditor1.LoadCanvas(flowName);
            svrName = "";
           
            flowControl = new FlowControl(MQTTControl.GetInstance(), startNodeName);
            flowControl.FlowCompleted += (s, e) =>
            {
                ButtonFlowOpen.Content = "开始流程";
                ButtonFlowPause.IsEnabled = false;
                ButtonFlowPause.Visibility = Visibility.Collapsed;
            };
            OperateGrid.Visibility = Visibility.Visible;
            this.Title = "流程编辑器 - " + new FileInfo(flowName).Name;
        }

        private void SaveFlow(string flowName,bool IsForceSave =false)
        {

            if (File.Exists(flowName)|| IsForceSave)
            {
                STNodeEditor1.SaveCanvas(flowName);
            }

            TemplateControl.GetInstance().SaveFlow2DB(FlowParam);
        }

        private string GetTopic()
        {
            return "SYS/CMD/" + startNodeName;
        }


        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            TextBoxsn.Text = softNumerical.GetNumericalOrder();
        }

        private async void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "开始流程")
                {
                    svrName = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                    FlowEngineLib.CVBaseDataFlow baseEvent = new FlowEngineLib.CVBaseDataFlow(svrName, "Start", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
                    
                    button.Content = "停止流程";
                    ButtonFlowPause.IsEnabled = true;
                    ButtonFlowPause.Visibility = Visibility.Visible;
                    ButtonFlowPause.Content = "暂停流程";
                }
                else
                {
                    CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Stop", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
                    button.Content = "开始流程";
                    ButtonFlowPause.IsEnabled = false;
                    ButtonFlowPause.Visibility = Visibility.Collapsed;

                }

            }

        }

        private async void Button_Click_5(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "暂停流程")
                {
                    CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Pause", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
                    button.Content = "恢复流程";
                }
                else
                {
                    CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Start", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
                    button.Content = "暂停流程";
                }
            }
        }


    }
}
