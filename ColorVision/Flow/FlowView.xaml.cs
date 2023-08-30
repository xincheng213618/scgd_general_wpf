using FlowEngineLib;
using ST.Library.UI.NodeEditor;
using System;
using System.Windows;
using System.Windows.Controls;

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
            STNodeEditorMain.ActiveChanged += (s, e) => STNodePropertyGrid1.SetNode(STNodeEditorMain.ActiveNode);
            flowEngine.AttachLoader(STNodeEditorMain);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualWidth > 200)
            {
                winf1.Height = (int)this.ActualHeight;
                winf1.Width = (int)this.ActualWidth - 200;
            }
        }
    }
}
