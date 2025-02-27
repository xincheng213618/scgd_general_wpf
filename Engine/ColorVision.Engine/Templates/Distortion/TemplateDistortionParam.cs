﻿using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Distortion
{
    public class TemplateDistortionParam : ITemplate<DistortionParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<DistortionParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DistortionParam>>();

        public TemplateDistortionParam()
        {
            Title = "畸变算法设置";
            Code = "distortion";
            TemplateParams = Params;
        }
    }
}
