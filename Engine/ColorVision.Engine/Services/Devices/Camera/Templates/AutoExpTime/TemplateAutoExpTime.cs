using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam
{
    public class TemplateAutoExpTime : ITemplate<AutoExpTimeParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<AutoExpTimeParam>> Params { get; set; } = new ObservableCollection<TemplateModel<AutoExpTimeParam>>();

        public TemplateAutoExpTime()
        {
            Title = "自动曝光模板设置";
            Code = "auto_exp_time";
            TemplateParams = Params;
        }

    }
}
