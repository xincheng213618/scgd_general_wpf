using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.BinocularFusion
{
    public class ExportBinocularFusion : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.StereoFusion;
        public override int Order => 2;
        public override ITemplate Template => new TemplateBinocularFusion();
    }
}
