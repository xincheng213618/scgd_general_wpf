using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.UI.Authorizations;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class DeviceSMU : DeviceService<ConfigSMU>
    {
        public MQTTSMU DService { get; set; }

        public ViewSMU View { get; set; }

        public DeviceSMU(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSMU(Config);
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
            }, a => AccessControl.Check(PermissionMode.Administrator));


        }
        public override UserControl GetDeviceInfo() => new InfoSMU(this);
        public override UserControl GetDisplayControl() => new DisplaySMUControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
