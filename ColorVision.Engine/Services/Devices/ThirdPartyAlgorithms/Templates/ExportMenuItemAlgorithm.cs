using ColorVision.UI.Menus;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{
    public class ExportMenuItemThirdPartyAlgorithms: MenuItemBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "ThirdPartyAlgorithms";
        public override string Header => "ThirdPartyAlgorithms";
        public override int Order => 3;
    }


}
