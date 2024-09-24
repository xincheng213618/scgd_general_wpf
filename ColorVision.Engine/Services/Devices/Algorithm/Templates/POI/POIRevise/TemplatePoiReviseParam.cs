using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIRevise
{
    public class TemplatePoiReviseParam : ITemplate<PoiReviseParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiReviseParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiReviseParam>>();

        public TemplatePoiReviseParam()
        {
            Title = "Poi修正算法设置";
            Code = "PoiRevise";
            TemplateParams = Params;
        }
    }
}
