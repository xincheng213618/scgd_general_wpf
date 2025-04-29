

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{
    public class MenuDisplayDistortion2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "畸变2";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateDistortion2();
    }

}
