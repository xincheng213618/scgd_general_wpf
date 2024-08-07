using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POIRevise
{
    public class TemplatePoiReviseParam : ITemplate<PoiReviseParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiReviseParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiReviseParam>>();

        public TemplatePoiReviseParam()
        {
            Title = "POICal算法设置";
            Code = "POICal";
            TemplateParams = Params;
        }
    }
}
