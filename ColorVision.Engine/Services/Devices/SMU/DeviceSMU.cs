using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Themes;
using ColorVision.Common.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.Util.Interfaces;

namespace ColorVision.Engine.Services.Devices.SMU
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
                EditSMU window = new(this);
                window.Icon = Icon;
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });


        }
        public override UserControl GetDeviceControl() => new InfoSMU(this);
        public override UserControl GetDeviceInfo() => new InfoSMU(this, false);
        public override UserControl GetDisplayControl() => new DisplaySMUControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return Service;
        }
    }
}
