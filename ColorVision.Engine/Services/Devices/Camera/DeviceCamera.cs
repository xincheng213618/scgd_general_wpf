using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes.Controls;
using ColorVision.UI.Authorizations;
using ColorVision.Util.Interfaces;
using log4net;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public class DeviceCamera : DeviceService<ConfigCamera>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCamera));

        public PhyCamera? PhyCamera { get; set; }
        public ViewCamera View { get; set; }
        public MQTTCamera DService { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }

        public RelayCommand FetchLatestTemperatureCommand { get; set; }
        public RelayCommand DisPlaySaveCommand { get; set; }

        public RelayCommand OpenCalibrationParamsCommand { get; set; }

        public DeviceCamera(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTCamera(Config);

            View = new ViewCamera(this);
            View.View.Title = $"相机视图 - {Config.Code}";
            this.SetIconResource("DrawingImageCamera", View.View);

            EditCommand = new RelayCommand(a => EditCameraAction() ,b => AccessControl.Check(EditCameraAction));

            OpenCalibrationParamsCommand = new RelayCommand(a =>
            {
            });
            FetchLatestTemperatureCommand =  new RelayCommand(a => FetchLatestTemperature(a));


            DisPlaySaveCommand = new RelayCommand(a => SaveDis());
            DisplayCameraControlLazy = new Lazy<DisplayCameraControl>(() => new DisplayCameraControl(this));


            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());
            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger());
            PhyCamera = PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraID);
            if (PhyCamera != null)
            {
                PhyCamera.ConfigChanged += PhyCameraConfigChanged;
                PhyCamera.DeviceCamera = this;
            }
        }

        private PhyCamera lastPhyCamera;

        public void PhyCameraConfigChanged(object? sender, PhyCameras.Configs.ConfigPhyCamera e)
        {
            if (lastPhyCamera !=null && sender is PhyCamera phyCamera && phyCamera != lastPhyCamera)
            {
                lastPhyCamera.ConfigChanged -= PhyCameraConfigChanged;
                lastPhyCamera = phyCamera;
                lastPhyCamera.DeviceCamera = this;
                lastPhyCamera.DeviceCamera = null;
            }
            Config.Channel = e.Channel;
            Config.CFW.CopyFrom(e.CFW);
            Config.MotorConfig.CopyFrom(e.MotorConfig);
            Config.CameraID = e.CameraID;
            Config.CameraType = e.CameraType;
            Config.CameraMode = e.CameraMode;
            Config.CameraModel = e.CameraModel;
            Config.TakeImageMode = e.TakeImageMode;
            Config.ImageBpp = e.ImageBpp;
            Save();
        }

        [RequiresPermission(PermissionMode.Administrator)]
        private void EditCameraAction()
        {
            EditCamera window = new(this);
            window.Owner = Application.Current.GetActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        public RelayCommand OpenPhyCameraMangerCommand { get; set; }

        [RequiresPermission(PermissionMode.Administrator)]
        public void OpenPhyCameraManger()
        {
            DService.GetAllCameraID();
            PhyCameraManagerWindow phyCameraManager = new() { Owner = Application.Current.GetActiveWindow() };
            phyCameraManager.Show();
        }

        public RelayCommand RefreshDeviceIdCommand { get; set; }

        public void RefreshDeviceId()
        {
            MsgRecord msgRecord =  DService.GetAllCameraID();
            msgRecord.MsgSucessed += (e) =>
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(),"GetAllCameraID Sucess");
                PhyCameraManager.GetInstance().LoadPhyCamera();
            };
        }

        public void SaveDis()
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), "是否保存当前界面的曝光配置", "ColorVison", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            SaveConfig();
        }

        public override void Save()
        {
            PhyCamera = PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraID);
            if (PhyCamera != null)
            {
                PhyCamera.SetDeviceCamera(this);
                NotifyPropertyChanged(nameof(PhyCamera));
            }
            base.Save();
        }

        private void FetchLatestTemperature(object a)
        {
            var model = CameraTempDao.Instance.GetLatestCameraTemp(SysResourceModel.Id);
            if (model != null)
            {
                MessageBox1.Show(Application.Current.MainWindow, $"{model.CreateDate:HH:mm:ss} {Environment.NewLine}温度:{model.TempValue}");
            }
            else
            {
                MessageBox1.Show(Application.Current.MainWindow, "查询不到对应的温度数据");
            }
        }

        public override UserControl GetDeviceInfo() => new InfoCamera(this);
        
        public Lazy<DisplayCameraControl> DisplayCameraControlLazy { get; set; }

        public override UserControl GetDisplayControl() => DisplayCameraControlLazy.Value;

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
        public override void Dispose()
        {
            DService?.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
