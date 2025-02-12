using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;

namespace ColorVision.Engine.Services.Devices.Sensor
{
    public class MenuTemplateSensor : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);

        public override int Order => 21;
        public override string Header => Properties.Resources.MenuSensor;
    }

}
