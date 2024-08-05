using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POICal
{
    public class TemplatePOICalParam : ITemplate<POICalParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<POICalParam>> Params { get; set; } = new ObservableCollection<TemplateModel<POICalParam>>();

        public TemplatePOICalParam()
        {
            Title = "POICal算法设置";
            Code = "POICal";
            TemplateParams = Params;
        }
    }
}
