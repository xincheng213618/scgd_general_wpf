using ColorVision.Database;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.ImageCropping
{
    public class TemplateImageCropping : ITemplate<ImageCroppingParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<ImageCroppingParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ImageCroppingParam>>();

        public TemplateImageCropping()
        {
            Title = ColorVision.Engine.Properties.Resources.ImageCroppingTemplateSettings;
            TemplateDicId = 32;
            Code = "ImageCropping";
            TemplateParams = Params;
        }

        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlImageCropping();
        }
    }
}
