using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF
{
    public class TemplateMTFParam : ITemplate<MTFParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<MTFParam>> Params { get; set; } = new ObservableCollection<TemplateModel<MTFParam>>();

        public TemplateMTFParam()
        {
            Title = "MTFParam算法设置";
            Code = "MTF";
            TemplateParams = TemplateMTFParam.Params;
        }
    }
}
