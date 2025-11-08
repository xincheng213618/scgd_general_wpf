using ColorVision.Database;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus
{
    public class TemplateAutoFocus : ITemplate<AutoFocusParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<AutoFocusParam>> Params { get; set; } = new ObservableCollection<TemplateModel<AutoFocusParam>>();

        public TemplateAutoFocus()
        {
            Name = "Camera,AutoFocus";
            TemplateDicId = 200;
            Title = ColorVision.Engine.Properties.Resources.AutoFocusTemplateSettings;
            Code = "AutoFocus";
            TemplateParams = Params;
        }

        public override IMysqlCommand? GetMysqlCommand() => new MysqAutoFocus();

    }
}
