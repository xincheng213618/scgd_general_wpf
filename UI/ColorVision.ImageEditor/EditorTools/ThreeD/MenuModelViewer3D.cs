using ColorVision.ImageEditor.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    public class MenuModelViewer3D : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Resources.MenuModelViewer3D;
        public override int Order => 10;

        public override void Execute()
        {
            new ModelViewer3DWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }
    }
}
