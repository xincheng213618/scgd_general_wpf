﻿using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF
{
    public class TemplateMTF : ITemplate<MTFParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<MTFParam>> Params { get; set; } = new ObservableCollection<TemplateModel<MTFParam>>();

        public TemplateMTF()
        {
            Title = "MTFParam算法设置";
            Code = "MTF";
            TemplateParams = TemplateMTF.Params;
        }
    }
}