using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.DataLoad
{
    public class TemplateDataLoad : ITemplate<DataLoadParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<DataLoadParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DataLoadParam>>();
        public TemplateDataLoad()
        {
            Title = "数据加载模板管理";
            Code = "DataLoad";
            TemplateParams = Params;
        }
    }
}
