using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
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

namespace ColorVision.Engine.Services
{
    public class MenuCacheSetting : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "缓存管理";
        public override int Order => 3;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new CacheSettingWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    /// <summary>
    /// CacheSettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CacheSettingWindow : Window
    {
        public CacheSettingWindow()
        {
            InitializeComponent();
        }
    }
}
