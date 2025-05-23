using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam
{
    public class TemplateCameraRunParam : ITemplate<CameraRunParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<CameraRunParam>> Params { get; set; } = new ObservableCollection<TemplateModel<CameraRunParam>>();

        public TemplateCameraRunParam()
        {
            Name = "Camera,Camera.RunParams";
            TemplateDicId = 20;
            Title = "相机参数模板";
            Code = "Camera.RunParams";
            TemplateParams = Params;
        }
        public override IMysqlCommand? GetMysqlCommand() => new MysqlCameraRunParam();
       
    }

}
