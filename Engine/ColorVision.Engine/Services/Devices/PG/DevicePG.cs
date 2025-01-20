using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Services.Devices.PG
{
    public class DevicePG : DeviceService<ConfigPG>
    {
        public MQTTPG DService { get; set; }

        public DevicePG(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTPG(Config);

            EditCommand = new RelayCommand(a =>
            {
                EditPG window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
        }
        public override UserControl GetDeviceInfo() => new InfoPG(this);

        public override UserControl GetDisplayControl() => new DisplayPG(this);

        public override MQTTServiceBase? GetMQTTService() => DService;
    }
}
