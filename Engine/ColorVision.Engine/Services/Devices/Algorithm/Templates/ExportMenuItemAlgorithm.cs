using ColorVision.UI.Menus;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates
{
    public class ExportMenuItemAlgorithm : MenuItemBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "TemplateAlgorithm";
        public override string Header => Properties.Resources.MenuAlgorithm;
        public override int Order => 3;
    }


}
