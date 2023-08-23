using FlowEngineLib;
using FlowEngineLib.Start;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision
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
            flowEngine = new FlowEngineControl(true);
            InitializeComponent();
            View = new View();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            STNodeEditorMain.ActiveChanged += (s, e) => STNodePropertyGrid1.SetNode(STNodeEditorMain.ActiveNode);
            flowEngine.AttachLoader(STNodeEditorMain);
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            winf1.Height = (int)this.ActualHeight;
            winf1.Width = (int)this.ActualWidth;

        }
    }
}
