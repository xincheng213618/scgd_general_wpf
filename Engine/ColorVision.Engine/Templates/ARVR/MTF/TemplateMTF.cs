using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.MTF
{
    public class TemplateMTF : ITemplate<MTFParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<MTFParam>> Params { get; set; } = new ObservableCollection<TemplateModel<MTFParam>>();

        public TemplateMTF()
        {
            Title = "MTF模板管理";
            Code = "MTF";
            TemplateParams = TemplateMTF.Params;
        }
    }
}
