using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.FOV
{
    public class TemplateFOV : ITemplate<FOVParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<FOVParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FOVParam>>();
        public TemplateFOV()
        {
            Title = "FOVParam算法设置";
            Code = "FOV";
            TemplateParams = Params;
        }
    }
}
