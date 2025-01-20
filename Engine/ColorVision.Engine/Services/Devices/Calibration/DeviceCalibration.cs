using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.PhyCameras;
using log4net;
using System;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class DeviceCalibration : DeviceService<ConfigCalibration>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCalibration));

        public MQTTCalibration DService { get; set; }

        public PhyCamera? PhyCamera { get => PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraCode); }

        public ViewCalibration View{ get; set; }

        public DeviceCalibration(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTCalibration(Config);
            View = new ViewCalibration(this);
            View.View.Title = $"校正视图 - {Config.Code}";
            this.SetIconResource("DICalibrationIcon", View.View);;

            EditCommand = new RelayCommand(a =>
            {
                EditCalibration window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger(),a => AccessControl.Check(OpenPhyCameraManger) && PhyCamera !=null);
            DisplayLazy = new Lazy<DisplayCalibrationControl>(() => new DisplayCalibrationControl(this));
            if (PhyCamera != null)
            {
                PhyCamera.ConfigChanged += PhyCameraConfigChanged;
                PhyCamera.DeviceCalibration = this;
            }
        }

        private PhyCamera lastPhyCamera;

        public void PhyCameraConfigChanged(object? sender, PhyCameras.Configs.ConfigPhyCamera e)
        {
            if (lastPhyCamera != null && sender is PhyCamera phyCamera && phyCamera != lastPhyCamera)
            {
                lastPhyCamera.ConfigChanged -= PhyCameraConfigChanged;
                lastPhyCamera = phyCamera;
                phyCamera.DeviceCalibration = this;
                lastPhyCamera.DeviceCalibration = null;
            }
            Save();
        }

        public RelayCommand OpenPhyCameraMangerCommand { get; set; }

        [RequiresPermission(PermissionMode.Administrator)]
        public static void OpenPhyCameraManger()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() }.Show();
        }

        public override void Save()
        {
            base.Save();
            if (PhyCamera != null)
                PhyCamera.SetCalibration(this);
        }


        public override UserControl GetDeviceInfo() => new InfoCalibration(this);

        readonly Lazy<DisplayCalibrationControl> DisplayLazy;

        public override UserControl GetDisplayControl() => DisplayLazy.Value;


        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
