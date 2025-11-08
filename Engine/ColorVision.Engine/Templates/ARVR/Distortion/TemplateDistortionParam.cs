using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Distortion
{
    public class TemplateDistortionParam : ITemplate<DistortionParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<DistortionParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DistortionParam>>();

        public TemplateDistortionParam()
        {
            Title = ColorVision.Engine.Properties.Resources.DistortionEvaluationTemplateManagement;
            TemplateDicId = 10;
            Code = "distortion";
            TemplateParams = Params;
        }
    }
}
