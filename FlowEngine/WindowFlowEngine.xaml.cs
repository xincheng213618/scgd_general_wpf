using FlowEngineLib;
using Newtonsoft.Json;
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
    public partial class WindowFlowEngine : Window
    {
        HslCommunication.BasicFramework.SoftNumericalOrder softNumerical;

        public WindowFlowEngine()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            STNodePropertyGrid1.Text = "属性";
            STNodeTreeView1.LoadAssembly("FlowEngineLib.dll");
            STNodeEditor1.LoadAssembly("FlowEngineLib.dll");
            STNodeEditor1.ActiveChanged += (s, e) => STNodePropertyGrid1.SetNode(STNodeEditor1.ActiveNode);
            STNodeEditor1.NodeAdded += StNodeEditor1_NodeAdded;
            softNumerical = new HslCommunication.BasicFramework.SoftNumericalOrder("CV", "yyyyMMddHH", 5, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\softNumerical.txt");
        }

        private void StNodeEditor1_NodeAdded(object sender, STNodeEditorEventArgs e)
        {
            STNode node = (STNode)e.Node;
            node.Tag = "223";
            node.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            node.ContextMenuStrip.Items.Add("删除", null, (s, e1) => STNodeEditor1.Nodes.Remove(node));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            STNodeEditor1.Nodes.Clear();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            STNodeEditor1.Nodes.Clear();
            STNodeEditor1.LoadCanvas(ofd.FileName);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
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

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            CVBaseDataFlow baseEvent = new CVBaseDataFlow("PG", "Start", softNumerical.GetNumericalOrder());
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {

        }
    }
}
