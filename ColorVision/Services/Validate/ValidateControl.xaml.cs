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

namespace ColorVision.Services.Validate
{
    /// <summary>
    /// ValidateControl.xaml 的交互逻辑
    /// </summary>
    public partial class ValidateControl : UserControl
    {
        public ValidateControl()
        {
            InitializeComponent();
        }

        public ValidateParam ValidateParam { get; set; }

        public void SetParam(ValidateParam param)
        {
            ValidateParam = param;
            this.DataContext = ValidateParam;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;  
        }
    }
}
