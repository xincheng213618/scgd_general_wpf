using FlowEngineLib;
using ST.Library.UI.NodeEditor;
using System;
using System.Windows;
using System.Windows.Controls;
using static OpenCvSharp.ML.DTrees;

namespace ColorVision.Flow
{
    /// <summary>
    /// FlowView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowView : UserControl, IView
    {
        private FlowEngineControl flowEngine;
        public FlowEngineControl FlowEngineControl { get { return flowEngine; } }

        public View View { get; set; }

        public FlowView()
        {
            flowEngine = new FlowEngineControl(false);
            InitializeComponent();
            View = new View();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            STNodePropertyGrid1.IsEditEnable = false;
            STNodeEditorMain.ActiveChanged += (s, e) => 
            {
                winf2.Visibility = STNodeEditorMain.ActiveNode == null ?Visibility.Collapsed: Visibility.Visible;
                STNodePropertyGrid1.SetNode(STNodeEditorMain.ActiveNode);
            };
            flowEngine.AttachLoader(STNodeEditorMain);

            STNodeEditorMain.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            STNodeEditorMain.ContextMenuStrip.Items.Add("设为主窗口", null, (s, e1) => ViewGridManager.GetInstance().SetOneView(this));
            STNodeEditorMain.ContextMenuStrip.Items.Add("显示全部窗口", null, (s, e1) => ViewGridManager.GetInstance().SetViewNum(-1));
            STNodeEditorMain.ContextMenuStrip.Items.Add("独立窗口中显示", null, (s, e1) =>  View.ViewIndex =-2);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualWidth > 200)
            {
                winf1.Height = (int)this.ActualHeight;
                winf1.Width = (int)this.ActualWidth;
            }
        }
    }
}
