﻿using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.ImageCropping
{
    public class TemplateImageCropping : ITemplate<ImageCroppingParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<ImageCroppingParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ImageCroppingParam>>();

        public TemplateImageCropping()
        {
            Title = "发光区裁剪模板";
            Code = "ImageCropping";
            TemplateParams = Params;
        }

        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlImageCropping();
        }
    }
}