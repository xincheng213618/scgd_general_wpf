using ColorVision.Engine.Services.Templates.POI.POIFix;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Templates.POI.POIFix
{
    public class TemplatePoiFix : ITemplate<PoiFixParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiFixParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiFixParam>>();

        public TemplatePoiFix()
        {
            Title = "PoiFix";
            Code = "PoiFix";
            TemplateParams = Params;
        }
    }
}
