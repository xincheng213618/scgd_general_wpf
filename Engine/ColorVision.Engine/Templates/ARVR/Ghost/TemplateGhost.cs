using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Ghost
{
    public class TemplateGhost : ITemplate<GhostParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<GhostParam>> Params { get; set; } = new ObservableCollection<TemplateModel<GhostParam>>();

        public TemplateGhost()
        {
            Title = "鬼影模板管理";
            Code = "ghost";
            TemplateParams = Params;
        }
    }


}
