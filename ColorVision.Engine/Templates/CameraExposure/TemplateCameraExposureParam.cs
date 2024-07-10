namespace ColorVision.Engine.Templates.CameraExposure
{
    public class TemplateCameraExposureParam : ITemplate<CameraExposureParam>, IITemplateLoad
    {
        public TemplateCameraExposureParam()
        {
            Title = "CameraExposureParam设置";
            Code = ModMasterType.CameraExposure;
            TemplateParams = CameraExposureParam.Params;
        }
    }

}
