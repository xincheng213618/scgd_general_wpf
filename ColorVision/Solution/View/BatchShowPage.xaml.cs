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

namespace ColorVision.Solution.View
{
    /// <summary>
    /// BatchShowPage.xaml 的交互逻辑
    /// </summary>
    public partial class BatchShowPage : Page
    {
        public Frame Frame { get; set; }
        public ViewBatchResult ViewBatchResult { get; set; }

        public BatchShowPage(Frame frame, ViewBatchResult viewBatchResult)
        {
            Frame = frame;
            ViewBatchResult = viewBatchResult;
            InitializeComponent();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {

        }
    }
}
