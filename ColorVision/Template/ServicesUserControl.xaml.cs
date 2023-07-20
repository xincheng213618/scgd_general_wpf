using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
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

namespace ColorVision.Template
{
    /// <summary>
    /// ServicesUserControl.xaml 的交互逻辑
    /// </summary>
    public partial class ServicesUserControl : UserControl
    {
        public ServicesUserControl()
        {
            InitializeComponent();

            SysDictionaryService sysDictionary = new SysDictionaryService();
            List<SysDictionaryModel> svrs = sysDictionary.GetAllServiceType();
            svrs.ForEach(service => { System.Console.WriteLine(service.Id); });
        }
    }
}
