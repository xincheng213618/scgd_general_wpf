using ColorVision.Database;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam
{
    public class TemplateCameraRunParam : ITemplate<CameraRunParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<CameraRunParam>> Params { get; set; } = new ObservableCollection<TemplateModel<CameraRunParam>>();
        private CameraRunParamEditor? _editor;

        public TemplateCameraRunParam()
        {
            Name = "Camera,Camera.RunParams";
            TemplateDicId = 20;
            Title = ColorVision.Engine.Properties.Resources.CameraParameterTemplate;
            Code = "Camera.RunParams";
            TemplateParams = Params;
            IsUserControl = true;
        }
        public override IMysqlCommand? GetMysqlCommand() => new MysqlCameraRunParam();

        public override UserControl GetUserControl() => _editor ??= new CameraRunParamEditor();

        public override UserControl CreateUserControl() => new CameraRunParamEditor();

        public override void SetUserControlDataContext(int index)
        {
            (_editor ??= new CameraRunParamEditor()).SetParam(TemplateParams[index].Value);
        }

    }

}
