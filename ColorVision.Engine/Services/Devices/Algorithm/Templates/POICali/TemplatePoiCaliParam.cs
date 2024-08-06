using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POICali
{
    public class TemplatePoiCaliParam : ITemplate<PoiCaliParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PoiCaliParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PoiCaliParam>>();

        public TemplatePoiCaliParam()
        {
            Title = "POICal算法设置";
            Code = "POICal";
            TemplateParams = Params;
        }
    }
}
