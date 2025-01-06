using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Services.Devices.FlowDevice
{

    public class DeviceFlowDevice : DeviceService<ConfigFlowDevice>
    {
        public MQTTFlowDevice DService { get; set; }
        public DeviceFlowDevice(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTFlowDevice(Config);
            EditCommand = new RelayCommand(a =>
            {
                EditFlowDevice window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        public override UserControl GetDeviceInfo() => new InfoFlowDevice(this);


        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
