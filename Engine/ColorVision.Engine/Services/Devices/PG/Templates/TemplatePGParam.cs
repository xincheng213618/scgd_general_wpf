
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.PG.Templates
{
    public class TemplatePGParam : ITemplate<PGParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<PGParam>> Params { get; set; } = new ObservableCollection<TemplateModel<PGParam>>();

        public TemplatePGParam()
        {
            Title = "PGParam设置";
            Code = "pg";
            TemplateDicId = 3;
            TemplateParams = Params;
        }
    }
}
