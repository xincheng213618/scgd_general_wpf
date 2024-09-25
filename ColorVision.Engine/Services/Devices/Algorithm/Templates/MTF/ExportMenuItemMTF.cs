using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF
{
    public class ExportMenuItemMTF : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "MTFParam";
        public override string Header => Properties.Resources.MenuMTF;
        public override int Order => 2;
        public override ITemplate Template => new TemplateMTF();
    }
}
