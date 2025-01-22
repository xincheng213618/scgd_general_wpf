using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraExposure
{
    public class TemplateCameraExposure : ITemplate<CameraExposureParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<CameraExposureParam>> Params { get; set; } = new ObservableCollection<TemplateModel<CameraExposureParam>>();

        public TemplateCameraExposure()
        {
            Title = "相机模板设置";
            Code = "camera_exp_time";
            TemplateParams = Params;
        }
        public override IMysqlCommand? GetMysqlCommand() => new MysqCameraExposure();

    }

}
