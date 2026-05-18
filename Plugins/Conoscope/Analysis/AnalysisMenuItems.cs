using ColorVision.UI.Menus;
using Conoscope.Properties;
using System.Windows;

namespace Conoscope.Analysis
{
    public class MenuContrastTestWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 51;
        public override string Header => Resources.MenuContrastTest;

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
        public override string Header => Resources.MenuGamutCalculation;

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