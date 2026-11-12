using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Motor
{
    public class DeviceMotor : DeviceService<ConfigMotor>,IIcon
    {
        public MQTTMotor DService { get; set; }
        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);

        public DeviceMotor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTMotor(Config);
            this.SetIconResource("COMDrawingImage");
          
            EditCommand = new RelayCommand(a =>
            {
                if (!CanConfigure) return;
                var propertyEditorWindow = new PropertyEditorWindow(Config, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                ProtectConfigurationEditor(propertyEditorWindow);
                propertyEditorWindow.Submitted += (s, e) => TryCommitConfiguration(propertyEditorWindow);
                propertyEditorWindow.ShowDialog();

            }, a => CanConfigure);
        }

        public override UserControl GetDeviceInfo() => new InfoMotor(this);

        public override UserControl GetDisplayControl() => new DisplayMotor(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
