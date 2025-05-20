

using ColorVision.Engine.Services.Devices.Camera.Templates;
using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.HDR
{
    public class MenuHDR : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuItemCamera);
        public override string Header => "HDR模板";
        public override int Order => 3;
        public override ITemplate Template => new TemplateHDR();
    }

}
