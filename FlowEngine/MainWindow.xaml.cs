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
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FlowEngine
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            STNodePropertyGrid1.Text = "Node_Property";

            //stNodeTreeView1.LoadAssembly(Application.ExecutablePath);
            STNodeTreeView1.LoadAssembly("FlowEngineLib.dll");
            STNodeEditor1.LoadAssembly("FlowEngineLib.dll");
            STNodeEditor1.ActiveChanged += (s, e) => STNodePropertyGrid1.SetNode(STNodeEditor1.ActiveNode);
            STNodeEditor1.NodeAdded += StNodeEditor1_NodeAdded;

        }

        private void StNodeEditor1_NodeAdded(object sender, STNodeEditorEventArgs e)
        {
            STNode node = (STNode)e.Node;
            node.Tag = "223";
        }
    }
}
