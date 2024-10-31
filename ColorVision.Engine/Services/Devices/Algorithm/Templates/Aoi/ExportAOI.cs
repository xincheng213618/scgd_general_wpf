using System.Windows;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Aoi
{
    public class ExportAOI : ExportTemplateBase
    {
        public override string GuidId => "AOIParam";
        public override string Header => "AOIParam";
        public override int Order => 1;
        public override Visibility Visibility => Visibility.Collapsed;
        public override ITemplate Template { get; } = new TemplateAOIParam();
    }
}
