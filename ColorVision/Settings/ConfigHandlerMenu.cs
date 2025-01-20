using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Windows;

namespace ColorVision.Settings
{
    public class ConfigHandlerMenu : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override string GuidId => nameof(ConfigHandlerMenu);

        public override string Header => "保存配置";

        public override int Order => 99998;

        public override void Execute()
        {
            try
            {
                ConfigHandler.GetInstance().SaveConfigs();
                MessageBox.Show(Application.Current.GetActiveWindow(), "保存成功");
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }
        }
    }
}
