using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.CADMapping
{
    public class ExportCADMapping : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "CADMapping";
        public override string Header => "CADMapping";
        public override int Order => 9;
        public override ITemplate Template => new TemplateCADMapping();
    }
}
