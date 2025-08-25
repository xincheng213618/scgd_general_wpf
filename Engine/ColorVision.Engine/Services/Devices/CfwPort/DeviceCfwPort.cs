using ColorVision.UI;
using ColorVision.Engine.Services.Core;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class DeviceCfwPort : DeviceService<ConfigCfwPort>
    {
        public MQTTCfwPort DService { get; set; }

        public DeviceCfwPort(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTCfwPort(Config);

            this.SetIconResource("CfwPortDrawingImage");

            EditCommand = new RelayCommand(a =>
            {
                EditCfwPort window = new EditCfwPort(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new InfoCfwPort(this);

        public override UserControl GetDisplayControl() => new DisplayCfwPort(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
