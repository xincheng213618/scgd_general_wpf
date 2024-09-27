using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi
{
    public class ExportBuildPOI : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "BuildPOI";
        public override string Header => "关注点布点";
        public override int Order => 9;

        public override ITemplate Template => new TemplateBuildPoi();
    }
}
