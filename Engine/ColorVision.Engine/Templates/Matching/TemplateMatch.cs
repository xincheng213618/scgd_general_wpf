using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Matching
{
    public class TemplateMatch : ITemplate<MatchParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<MatchParam>> Params { get; set; } = new ObservableCollection<TemplateModel<MatchParam>>();

        public TemplateMatch()
        {
            Title = "MTFParam算法设置";
            Code = "MatchTemplate";
            TemplateParams = Params;
        }
    }
}
