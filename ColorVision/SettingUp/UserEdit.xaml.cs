using ColorVision.Controls;
using ColorVision.Extension;

using NPOI.Util;
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
using System.Windows.Shapes;

namespace ColorVision.SettingUp
{
    /// <summary>
    /// UserEdit.xaml 的交互逻辑
    /// </summary>
    public partial class UserEdit : BaseWindow
    {
        public UserConfig UserConfig { get; set; }
        public UserConfig UserConfigCopy { get; set; }

        public UserEdit(UserConfig userConfig)
        {
            UserConfig = userConfig;
            UserConfigCopy = new UserConfig();
            //UserConfig?.CopyTo(UserConfigCopy);
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            //UserConfigCopy.CopyTo(UserConfig);
        }
    }
}
