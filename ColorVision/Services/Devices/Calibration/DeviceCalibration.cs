using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Extension;
using ColorVision.Interfaces;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Calibration.Views;
using ColorVision.Services.Devices.PG;
using ColorVision.Services.Msg;
using ColorVision.Services.PhyCameras;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.RC;
using ColorVision.Services.Templates;
using ColorVision.Services.Types;
using ColorVision.Solution;
using ColorVision.Themes.Controls;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Calibration
{
    public class DeviceCalibration : DeviceService<ConfigCalibration>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCalibration));

        public MQTTCalibration DeviceService { get; set; }

        public PhyCamera? PhyCamera { get => PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraID); }

        public ViewCalibration View{ get; set; }

        public DeviceCalibration(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTCalibration(Config);
            View = new ViewCalibration(this);
            View.View.Title = $"校正视图 - {Config.Code}";
            this.SetIconResource("DICalibrationIcon", View.View);;

            EditCommand = new RelayCommand(a =>
            {
                EditCalibration window = new EditCalibration(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });

            DisplayLazy = new Lazy<DisplayCalibrationControl>(() => new DisplayCalibrationControl(this));
        }
        public override void Save()
        {
            base.Save();
            if (PhyCamera != null)
                PhyCamera.SetCalibration(this);
        }

        public override UserControl GetDeviceControl() => new InfoCalibration(this);

        public override UserControl GetDeviceInfo() => new InfoCalibration(this,false);

        readonly Lazy<DisplayCalibrationControl> DisplayLazy;

        public override UserControl GetDisplayControl() => DisplayLazy.Value;


        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
