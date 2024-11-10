using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraExposure
{
    public class TemplateCameraExposureParam : ITemplate<CameraExposureParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<CameraExposureParam>> Params { get; set; } = new ObservableCollection<TemplateModel<CameraExposureParam>>();

        public TemplateCameraExposureParam()
        {
            Title = "CameraExposureParam设置";
            Code = "camera_exp_time";
            TemplateParams = Params;
        }
    }

}
