using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.MTF
{
    public class TemplateMTF : ITemplate<MTFParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<MTFParam>> Params { get; set; } = new ObservableCollection<TemplateModel<MTFParam>>();

        public TemplateMTF()
        {
            Title = ColorVision.Engine.Properties.Resources.MTFTemplateManagement;
            TemplateDicId = 8;
            Code = "MTF";
            TemplateParams = TemplateMTF.Params;
        }
    }
}
