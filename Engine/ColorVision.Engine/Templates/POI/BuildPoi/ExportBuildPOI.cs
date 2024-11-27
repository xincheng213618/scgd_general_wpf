namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class ExportBuildPOI : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "BuildPOI";
        public override string Header => "Poi布点模板设置";
        public override int Order => 5;

        public override ITemplate Template => new TemplateBuildPoi();
    }
}
