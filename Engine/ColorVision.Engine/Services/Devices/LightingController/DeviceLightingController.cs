using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.LightingController
{
    public class DeviceLightingController : DeviceService<ConfigLightingController>
    {
        public MQTTLightingController DService { get; }

        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);

        public DeviceLightingController(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTLightingController(Config);
            this.SetIconResource("COMDrawingImage");

            EditCommand = new RelayCommand(_ =>
            {
                PropertyEditorWindow window = new(Config, PropertyEditorEditMode.Transactional)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                window.Submitted += (_, _) => Save();
                window.ShowDialog();
            }, _ => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new InfoLightingController(this);

        public override UserControl GetDisplayControl() => new DisplayLightingController(this);

        public override MQTTServiceBase GetMQTTService() => DService;

        public override void Dispose()
        {
            DService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
