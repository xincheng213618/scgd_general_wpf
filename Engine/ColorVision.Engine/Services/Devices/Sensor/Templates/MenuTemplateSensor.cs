using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public class MenuTemplateSensor : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 13;
        public override string Header => Properties.Resources.MenuSensor;
    }

}
