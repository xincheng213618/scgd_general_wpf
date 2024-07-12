using ColorVision.Util.Interfaces;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Services.Devices.CfwPort
{
    public class DeviceCfwPort : DeviceService<ConfigCfwPort>
    {
        public MQTTCfwPort DService { get; set; }

        public DeviceCfwPort(SysDeviceModel sysResourceModel) : base(sysResourceModel)
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

        public override UserControl GetDeviceControl() => new InfoCfwPort(this);
        public override UserControl GetDeviceInfo() => new InfoCfwPort(this);

        public override UserControl GetDisplayControl() => new DisplayCfwPortControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
