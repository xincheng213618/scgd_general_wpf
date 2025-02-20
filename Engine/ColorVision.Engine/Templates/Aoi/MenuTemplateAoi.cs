using System.Windows;

namespace ColorVision.Engine.Templates.Aoi
{
    public class MenuTemplateAoi : MenuITemplateAlgorithmBase
    {
        public override string Header => "AOIParam";
        public override int Order => 1;
        public override Visibility Visibility => Visibility.Collapsed;
        public override ITemplate Template { get; } = new TemplateAOIParam();
    }
}
