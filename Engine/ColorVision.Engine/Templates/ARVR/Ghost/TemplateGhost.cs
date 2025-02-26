using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Ghost
{
    public class TemplateGhost : ITemplate<GhostParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<GhostParam>> Params { get; set; } = new ObservableCollection<TemplateModel<GhostParam>>();

        public TemplateGhost()
        {
            Title = "GhostParam算法设置";
            Code = "ghost";
            TemplateParams = Params;
        }
    }


}
