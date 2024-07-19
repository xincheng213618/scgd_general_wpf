using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.DataLoad
{
    public class TemplateDataLoadParam : ITemplate<DataLoadParam>, IITemplateLoad
    {
        public TemplateDataLoadParam()
        {
            Title = "数据加载算法设置";
            Code = "DataLoad";
            TemplateParams = DataLoadParam.Params;
        }
    }
}
