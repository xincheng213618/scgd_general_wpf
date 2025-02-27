using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Matching
{
    public class TemplateMatch : ITemplate<MatchParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<MatchParam>> Params { get; set; } = new ObservableCollection<TemplateModel<MatchParam>>();

        public TemplateMatch()
        {
            Title = "模板匹配模板管理";
            Code = "MatchTemplate";
            TemplateParams = Params;
        }
    }
}
