using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi
{
    public class TemplateBuildPoi : ITemplate<ParamBuildPoi>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<ParamBuildPoi>> Params { get; set; } = new ObservableCollection<TemplateModel<ParamBuildPoi>>();


        public TemplateBuildPoi()
        {
            Title = "BuildPOI算法设置";
            Code = "BuildPOI";
            TemplateParams = Params;
        }
    }
}
