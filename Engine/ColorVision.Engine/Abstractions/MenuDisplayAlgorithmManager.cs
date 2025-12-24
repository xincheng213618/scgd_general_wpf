using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine
{
    public class MenuDisplayAlgorithmManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "显示算法管理";
        public override int Order => 4;

        public override void Execute()
        {
            DisplayAlgorithmManager.GetInstance().Edit();
        }
    }
}
