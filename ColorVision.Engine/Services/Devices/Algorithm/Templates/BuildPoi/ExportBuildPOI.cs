using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi
{
    public class ExportBuildPOI : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "TemplatePOI";
        public override string Header => Properties.Resources.MenuBuildPOI;
        public override int Order => 0;

        public override ITemplate Template => new TemplateBuildPOIParam();
    }
}
