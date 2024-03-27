using ColorVision.Common.MVVM;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Common.Utilities;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.PG
{
    public class DevicePG : DeviceService<ConfigPG>
    {
        public MQTTPG DeviceService { get; set; }

        public DevicePG(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTPG(Config);

            EditCommand = new RelayCommand(a =>
            {
                EditPG window = new EditPG(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });
        }

        public override UserControl GetDeviceControl() => new DevicePGControl(this);
        public override UserControl GetDeviceInfo() => new DevicePGControl(this, false);

        public override UserControl GetDisplayControl() => new DisplayPGControl(this);
        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
        public string IsNet
        {
            get
            {
                if (Config.IsNet) { return "网络"; }
                else { return "串口"; }
            }
            set { }
        }
    }
}
