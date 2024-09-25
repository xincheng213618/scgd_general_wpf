using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi
{
    public class TemplateBuildPOIParam : ITemplate<BuildPOIParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<BuildPOIParam>> Params { get; set; } = new ObservableCollection<TemplateModel<BuildPOIParam>>();


        public TemplateBuildPOIParam()
        {
            Title = "BuildPOI算法设置";
            Code = "BuildPOI";
            TemplateParams = Params;
        }
    }
}
