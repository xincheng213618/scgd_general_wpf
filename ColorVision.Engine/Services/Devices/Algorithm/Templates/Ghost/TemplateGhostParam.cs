using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost
{
    public class TemplateGhostParam : ITemplate<GhostParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<GhostParam>> Params { get; set; } = new ObservableCollection<TemplateModel<GhostParam>>();

        public TemplateGhostParam()
        {
            Title = "GhostParam算法设置";
            Code = ModMasterType.Ghost;
            TemplateParams = Params;
        }
    }


}
