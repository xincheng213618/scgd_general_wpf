using ColorVision.Engine.Services.SysDictionary;
using ColorVision.Engine.ThirdPartyAlgorithms.Devices.ThirdPartyAlgorithms.Templates.FindDotsArray;
using ColorVision.UI.Sorts;
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

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{
    /// <summary>
    /// EditTemplateThird.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateThird : UserControl
    {
        public EditTemplateThird()
        {
            InitializeComponent();
        }
        public FindDotsArrayParam Param { get; set; }

        public void SetParam(FindDotsArrayParam param)
        {
            Param = param;
            this.DataContext = Param;
        }
    }
}
