#pragma warning disable CA1707
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost
{
    public class ExportGhostParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "GhostParam";
        public override string Header => Properties.Resources.MenuGhost;
        public override int Order => 3;
        public override ITemplate Template => new TemplateGhostParam();
    }


}
