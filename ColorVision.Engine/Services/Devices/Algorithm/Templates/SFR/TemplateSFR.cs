using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR
{
    public class TemplateSFR : ITemplate<SFRParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<SFRParam>> Params { get; set; } = new ObservableCollection<TemplateModel<SFRParam>>();

        public TemplateSFR()
        {
            Title = "SFRParam算法设置";
            Code = "SFR";
            TemplateParams = Params;
        }
    }
}
