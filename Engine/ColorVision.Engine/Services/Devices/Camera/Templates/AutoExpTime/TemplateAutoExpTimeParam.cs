using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam
{
    public class TemplateAutoExpTimeParam : ITemplate<AutoExpTimeParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<AutoExpTimeParam>> Params { get; set; } = new ObservableCollection<TemplateModel<AutoExpTimeParam>>();

        public TemplateAutoExpTimeParam()
        {
            Title = "自动曝光模板设置";
            Code = "auto_exp_time";
            TemplateParams = Params;
        }
    }
}
