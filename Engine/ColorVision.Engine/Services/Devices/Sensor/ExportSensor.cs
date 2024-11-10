using ColorVision.UI.Menus;

namespace ColorVision.Engine.Services.Devices.Sensor
{
    public class ExportSensor : MenuItemBase
    {
        public override string OwnerGuid => "Template";

        public override string GuidId => "TemplateSensor";
        public override int Order => 21;
        public override string Header => Properties.Resources.MenuSensor;
    }

}
