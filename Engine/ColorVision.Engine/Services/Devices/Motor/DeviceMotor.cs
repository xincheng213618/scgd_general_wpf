using ColorVision.Common.MVVM;
using ColorVision.Engine.Extension;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Motor
{
    public class DeviceMotor : DeviceService<ConfigMotor>,IIcon
    {
        public MQTTMotor DService { get; set; }
        public IDisPlayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisPlayConfigBase>(Config.Code);

        public DeviceMotor(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTMotor(Config);
            this.SetIconResource("COMDrawingImage");
          
            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();

            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new InfoMotor(this);

        public override UserControl GetDisplayControl() => new DisplayMotor(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
