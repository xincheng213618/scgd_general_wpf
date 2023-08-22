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
    public partial class FlowView : UserControl
    {
        public FlowView()
        {
            InitializeComponent();
        }

        internal void LoadFile(string fileName)
        {
            STNodeEditorMain.Nodes.Clear();
            STNodeEditorMain.LoadCanvas(fileName);
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
        }
    }
}
