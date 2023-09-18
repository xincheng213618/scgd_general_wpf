using ColorVision.MQTT;
using ColorVision.SettingUp;
using ColorVision.Solution;
using ColorVision.Template;
using FlowEngineLib;
using FlowEngineLib.Start;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class WindowFlowEngine : Window
    {
        public bool IsSave { get; set; } = true;

        public WindowFlowEngine()
        {
            InitializeComponent();
            ButtonSave.Visibility = Visibility.Collapsed;
            ButtonClear.Visibility = Visibility.Collapsed;
        }

        public WindowFlowEngine(string FileName)
        {
            InitializeComponent();
            string fileNameFull = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.GetFullFileName(FileName);
            if (File.Exists(fileNameFull))
            {
                OpenFlow(fileNameFull);
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
        public WindowFlowEngine(FlowParam flowParam) : this(flowParam.FileName??string.Empty)
        {
            FlowParam = flowParam;
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX + 100, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX - 100, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY + 100, bAnimation: true, CanvasMoveArgs.Top);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY - 100, bAnimation: true, CanvasMoveArgs.Top);
                e.Handled = true;
            }
            else if (e.Key == Key.Add)
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.1f, (STNodeEditorMain.Width / 2), (STNodeEditorMain.Height / 2));
                e.Handled = true;
            }
            else if (e.Key == Key.Subtract)
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.1f, (STNodeEditorMain.Width / 2), (STNodeEditorMain.Height / 2));
                e.Handled = true;
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            STNodePropertyGrid1.Text = "属性";
            STNodeTreeView1.LoadAssembly("FlowEngineLib.dll");
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            STNodeEditorMain.ActiveChanged += (s, e) => STNodePropertyGrid1.SetNode(STNodeEditorMain.ActiveNode);
            STNodeEditorMain.NodeAdded += StNodeEditor1_NodeAdded;
            ;
            STNodeEditorMain.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Delete)
                {
                    if (STNodeEditorMain.ActiveNode !=null)
                        STNodeEditorMain.Nodes.Remove(STNodeEditorMain.ActiveNode);
                    foreach (var item in STNodeEditorMain.GetSelectedNode())
                    {
                        STNodeEditorMain.Nodes.Remove(item);
                    }
                }
            };
            TextBoxsn.Text = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            this.Closed +=(s,e)=>
            {
                if (IsSave)
                {
                    //SaveFlow(FileName);
                }
                else
                {
                    if (MessageBox.Show("您是否保存流程", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                        ofd.Filter = "*.stn|*.stn";
                        ofd.FileName = FileName;
                        ofd.InitialDirectory = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.SolutionFullName;
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
            node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditorMain.Nodes.Remove(node));
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            SaveFlow(FileName);
            STNodeEditorMain.Nodes.Clear();
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
                FlowParam.FileName = FlowParam.Name + ".stn";
                FileName = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.SolutionFullName + "\\" + FlowParam.FileName;
                SaveFlow(FileName, true);
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
                STNodeEditorMain.Nodes.Clear();
        }

        string FileName { get; set; }

        private string svrName;

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            ofd.InitialDirectory = SolutionControl.GetInstance().SolutionConfig.SolutionFullName;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            ButtonSave.Visibility = Visibility.Visible;
            OpenFlow(ofd.FileName);
        }
        FlowControl flowControl;

        private void OpenFlow(string flowName)
        {
            FileName = flowName;
            STNodeEditorMain.Nodes.Clear();
            STNodeEditorMain.LoadCanvas(flowName);
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
                STNodeEditorMain.SaveCanvas(flowName);
            }

            TemplateControl.GetInstance().Save2DB(FlowParam);
        }

        private string GetTopic()
        {
            return "FLOW/CMD/" + startNodeName;
        }


        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            TextBoxsn.Text = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
        }

        private async void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "开始流程")
                {
                    svrName = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                    CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Start", TextBoxsn.Text);
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

        private void Button_Click_Stop(object sender, RoutedEventArgs e)
        {

        }
    }
}
