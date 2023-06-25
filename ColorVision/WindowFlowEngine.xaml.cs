using ColorVision.MQTT;
using FlowEngineLib;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
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
        HslCommunication.BasicFramework.SoftNumericalOrder softNumerical;

        public WindowFlowEngine()
        {
            InitializeComponent();
        }
        public WindowFlowEngine(string FileName)
        {
            InitializeComponent();
            OpenFlow(FileName);
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
            softNumerical = new HslCommunication.BasicFramework.SoftNumericalOrder("CV", "yyyyMMddHH", 5, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\softNumerical.txt");
            TextBoxsn.Text = softNumerical.GetNumericalOrder();

            string iPStr = "192.168.3.225";
            int port = 1883;
            string uName = "";
            string uPwd = "";

            FlowEngineLib.MQTTHelper.SetDefaultCfg(iPStr, port, uName, uPwd);
        }


        private void StNodeEditor1_NodeAdded(object sender, STNodeEditorEventArgs e)
        {
            STNode node = (STNode)e.Node;
            node.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditor1.Nodes.Remove(node));
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            STNodeEditor1.Nodes.Clear();
        }
        private string svrName;

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            OpenFlow(ofd.FileName);
        }
        FlowControl flowControl;
        private void OpenFlow(string flowName)
        {
            STNodeEditor1.Nodes.Clear();
            STNodeEditor1.LoadCanvas(flowName);
            svrName = "";
           
            flowControl = new FlowControl(MQTTControl.GetInstance(), "1");
            flowControl.FlowCompleted += (s, e) =>
            {
                ButtonFlowOpen.Content = "开始流程";
                ButtonFlowPause.IsEnabled = false;
                ButtonFlowPause.Visibility = Visibility.Collapsed;
            };
            OperateGrid.Visibility = Visibility.Visible;
        }

        private void Button_Click_Save(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
            sfd.Filter = "*.stn|*.stn";
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            STNodeEditor1.SaveCanvas(sfd.FileName);
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
                    await MQTTControl.GetInstance().PublishAsyncClient("SYS.CMD.1", JsonConvert.SerializeObject(baseEvent), false);
                    
                    button.Content = "停止流程";
                    ButtonFlowPause.IsEnabled = true;
                    ButtonFlowPause.Visibility = Visibility.Visible;
                    ButtonFlowPause.Content = "暂停流程";
                }
                else
                {
                    CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Stop", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient("SYS.CMD.1", JsonConvert.SerializeObject(baseEvent), false);
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
                    await MQTTControl.GetInstance().PublishAsyncClient("SYS.CMD.1", JsonConvert.SerializeObject(baseEvent), false);
                    button.Content = "恢复流程";
                }
                else
                {
                    CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Start", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient("SYS.CMD.1", JsonConvert.SerializeObject(baseEvent), false);
                    button.Content = "暂停流程";
                }
            }
        }

    }
}
