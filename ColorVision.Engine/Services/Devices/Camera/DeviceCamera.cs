﻿using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Interfaces;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.Services.Msg;
using ColorVision.Services.PhyCameras;
using log4net;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Camera
{
    public class DeviceCamera : DeviceService<ConfigCamera>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCamera));

        public PhyCamera? PhyCamera { get => PhyCameraManager.GetInstance().GetPhyCamera(Config.CameraID);}

        public ViewCamera View { get; set; }
        public MQTTCamera DeviceService { get; set; }
        public MQTTTerminalCamera Service { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }

        public RelayCommand FetchLatestTemperatureCommand { get; set; }
        public RelayCommand DisPlaySaveCommand { get; set; }

        public RelayCommand OpenCalibrationParamsCommand { get; set; }

        public DeviceCamera(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTCamera(Config);

            View = new ViewCamera(this);
            View.View.Title = $"相机视图 - {Config.Code}";
            this.SetIconResource("DrawingImageCamera", View.View);

            EditCommand = new RelayCommand(a =>
            {
                EditCamera window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });

            OpenCalibrationParamsCommand = new RelayCommand(a =>
            {

            });

            FetchLatestTemperatureCommand =  new RelayCommand(a => FetchLatestTemperature(a));


            DisPlaySaveCommand = new RelayCommand(a => SaveDis());
            DisplayCameraControlLazy = new Lazy<DisplayCameraControl>(() => new DisplayCameraControl(this));

            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());
            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger());
        }




        public RelayCommand OpenPhyCameraMangerCommand { get; set; }
        public void OpenPhyCameraManger()
        {
            DeviceService.GetAllCameraID();
            PhyCameraManagerWindow phyCameraManager = new() { Owner = Application.Current.GetActiveWindow() };
            phyCameraManager.Show();
        }

        public RelayCommand RefreshDeviceIdCommand { get; set; }

        public void RefreshDeviceId()
        {
            MsgRecord msgRecord =  DeviceService.GetAllCameraID();
            msgRecord.MsgSucessed += (e) =>
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),"GetAllCameraID Sucess");
            };
        }

        public void SaveDis()
        {
            if (MessageBox.Show(Application.Current.GetActiveWindow(), "是否保存当前界面的曝光配置", "ColorVison", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            SaveConfig();
        }

        public override void Save()
        {
            if (PhyCamera != null)
            {
                PhyCamera.SetDeviceCamera(this);
                Config.IsChannelThree = PhyCamera.Config.IsChannelThree;
                PhyCamera.Config.CameraCfg.CopyTo(Config.CameraCfg);
                PhyCamera.Config.CFW.CopyTo(Config.CFW);
            }
            base.Save();
        }

        private void FetchLatestTemperature(object a)
        {
            var model = CameraTempDao.Instance.GetLatestCameraTemp(SysResourceModel.Id);
            if (model != null)
            {
                MessageBox.Show(Application.Current.MainWindow, $"{model.CreateDate:HH:mm:ss} {Environment.NewLine}温度:{model.TempValue}");
            }
            else
            {
                MessageBox.Show(Application.Current.MainWindow, "查询不到对应的温度数据");
            }
        }

        public override UserControl GetDeviceControl() => new InfoCamera(this);
        public override UserControl GetDeviceInfo() => new InfoCamera(this, false);
        
        public Lazy<DisplayCameraControl> DisplayCameraControlLazy { get; set; }

        public override UserControl GetDisplayControl() => DisplayCameraControlLazy.Value;

        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
        public override void Dispose()
        {
            Service.Dispose();
            DeviceService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}