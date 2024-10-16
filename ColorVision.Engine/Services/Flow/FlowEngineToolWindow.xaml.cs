﻿using ColorVision.Engine.MQTT;
using ColorVision.Engine.Properties;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Flow
{
    public class ExportFlowEngine : MenuItemBase
    {
        public override string OwnerGuid => "Tool";
        public override string GuidId => "FlowEngine";
        public override string Header => Resources.WorkflowEngine;
        public override int Order => 3;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new FlowEngineToolWindow() { WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class FlowEngineToolWindow : Window
    {

        public FlowEngineToolWindow()
        {
            InitializeComponent();
            ButtonOpen.Visibility = Visibility.Collapsed;
            ButtonNew.Visibility = Visibility.Collapsed;
            this.ApplyCaption();
        }
        FlowParam FlowParam { get; set; }
        public FlowEngineToolWindow(FlowParam flowParam) : this()
        {
            FlowParam = flowParam;
            OpenFlowBase64(flowParam);
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

            this.Closed += (s, e) =>
            {
                if (AutoSave())
                {
                    if (MessageBox.Show("是否保存修改", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        SaveFlow();
                    }

                }
            };
        }

        //private string startNodeName;
        private BaseStartNode nodeStart;
        private void StNodeEditor1_NodeAdded(object sender, STNodeEditorEventArgs e)
        {
            STNode node = e.Node;
            if (e.Node != null && e.Node is BaseStartNode nodeStart)
            {
                //this.startNodeName = nodeStart.NodeName;
                this.nodeStart = nodeStart;
            }
            node.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditorMain.Nodes.Remove(node));
        }

        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog ofd = new();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            SaveFlow();
            STNodeEditorMain.Nodes.Clear();
        }

        private void Button_Click_Save(object sender, RoutedEventArgs e)
        {
            SaveFlow();
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
            System.Windows.Forms.OpenFileDialog ofd = new();
            ofd.Filter = "*.stn|*.stn";
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            ButtonSave.Visibility = Visibility.Visible;
            OpenFlow(ofd.FileName);
        }
        FlowControl flowControl;

        public void OpenFlow(string flowName)
        {
            FileName = flowName;
            STNodeEditorMain.Nodes.Clear();
            STNodeEditorMain.LoadCanvas(flowName);
            svrName = "";
            if (nodeStart != null)
            {
                flowControl = new FlowControl(MQTTControl.GetInstance(), nodeStart.NodeName);
                flowControl.FlowCompleted += (s, e) =>
                {
                    ButtonFlowOpen.Content = "开始流程";
                    ButtonFlowPause.IsEnabled = false;
                    ButtonFlowPause.Visibility = Visibility.Collapsed;
                };
            }
            OperateGrid.Visibility = Visibility.Visible;
            Title = "流程编辑器 - " + new FileInfo(flowName).Name;
        }

        public void OpenFlowBase64(FlowParam flowParam)
        {
            FileName = flowParam.Name;
            STNodeEditorMain.Nodes.Clear();
            if (!string.IsNullOrEmpty(flowParam.DataBase64))
            {
                STNodeEditorMain.LoadCanvas(Convert.FromBase64String(flowParam.DataBase64));
            }
            svrName = "";
            if (nodeStart != null)
            {
                flowControl = new FlowControl(MQTTControl.GetInstance(), nodeStart.NodeName);
                flowControl.FlowCompleted += (s, e) =>
                {
                    ButtonFlowOpen.Content = "开始流程";
                    ButtonFlowPause.IsEnabled = false;
                    ButtonFlowPause.Visibility = Visibility.Collapsed;
                };
            }
            else
            {
            }
            OperateGrid.Visibility = Visibility.Visible;
            Title = "流程编辑器 - " + new FileInfo(flowParam.Name).Name;
        }

        private bool AutoSave()
        {
            if (FlowParam == null) return false;
            if (nodeStart != null) { if (!nodeStart.Ready) { MessageBox.Show("保存失败！流程存在错误!!!"); return false; } }
            var data = STNodeEditorMain.GetCanvasData();

            string base64 = Convert.ToBase64String(data);
            return FlowParam.DataBase64.Length != base64.Length;
        }

        private void SaveFlow()
        {
            if (nodeStart != null) { if (!nodeStart.Ready) { MessageBox.Show("保存失败！流程存在错误!!!"); return; } }
            var data = STNodeEditorMain.GetCanvasData();

            if (FlowParam != null)
            {
                FlowParam.DataBase64 = Convert.ToBase64String(data);
                FlowParam.Save2DB(FlowParam);
            }

            MessageBox.Show("保存成功");
        }

        private string GetTopic()
        {
            return "FLOW/CMD/" + nodeStart?.NodeName;
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
                    CVBaseDataFlow baseEvent = new(svrName, "Start", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
                    
                    button.Content = "停止流程";
                    ButtonFlowPause.Visibility = Visibility.Visible;
                    ButtonFlowPause.Content = "暂停流程";
                }
                else
                {
                    CVBaseDataFlow baseEvent = new(svrName, "Stop", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
                    button.Content = "开始流程";
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
                    CVBaseDataFlow baseEvent = new(svrName, "Pause", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
                    button.Content = "恢复流程";
                }
                else
                {
                    CVBaseDataFlow baseEvent = new(svrName, "Start", TextBoxsn.Text);
                    await MQTTControl.GetInstance().PublishAsyncClient(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
                    button.Content = "暂停流程";
                }
            }
        }

        private void Button_Click_Stop(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_AlignTop(object sender, RoutedEventArgs e)
        {
            STNodeEditorMain.AlignTop();
        }

        private void Button_Click_AlignDis(object sender, RoutedEventArgs e)
        {
            STNodeEditorMain.AlignHorizontalDistance();
        }
    }
}
