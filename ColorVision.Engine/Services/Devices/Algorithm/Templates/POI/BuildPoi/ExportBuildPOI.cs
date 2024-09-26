using ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi
{
    public class ExportBuildPOI : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "BuildPOI";
        public override string Header => Properties.Resources.MenuBuildPOI;
        public override int Order => 0;

        public override ITemplate Template => new TemplateBuildPoi();
    }
}
