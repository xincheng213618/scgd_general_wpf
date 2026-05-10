using ColorVision.UI.Menus;
using System.Windows;

namespace Conoscope.Analysis
{
    public class MenuContrastTestWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 51;
        public override string Header => "对比度测试";

        public override void Execute()
        {
            ContrastTestWindow window = new()
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
        }
    }

    public class MenuColorGamutWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 52;
        public override string Header => "色域计算";

        public override void Execute()
        {
            ColorGamutWindow window = new()
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show();
        }
    }
}