using ColorVision.Common.MVVM;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.Services.Devices.SMU.Configs;
using ColorVision.Services.Devices.SMU.Views;
using ColorVision.Services.Extension;
using ColorVision.Themes;
using ColorVision.Common.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Devices.SMU
{
    public class DeviceSMU : DeviceService<ConfigSMU>
    {
        public MQTTSMU Service { get; set; }

        public ViewSMU View { get; set; }

        public DeviceSMU(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new MQTTSMU(Config);
            View = new ViewSMU();
            View.View.Title = $"源表视图 - {Config.Code}";
            this.SetIconResource("SMUDrawingImage", View.View);


            EditCommand = new RelayCommand(a =>
            {
                EditSMU window = new EditSMU(this);
                window.Icon = Icon;
                window.Owner = WindowHelpers.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });


        }
        public override UserControl GetDeviceControl() => new DeviceSMUControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSMUControl(this, false);
        public override UserControl GetDisplayControl() => new DisplaySMUControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return Service;
        }
    }
}
