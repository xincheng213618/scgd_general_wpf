using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.Services.Extension;
using ColorVision.Services.Msg;
using ColorVision.Services.PhyCameras;
using ColorVision.Services.PhyCameras.Dao;
using ColorVision.Services.Templates;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Camera
{
    public class DeviceCamera : DeviceService<ConfigCamera>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCamera));

        public DeviceCalibration? DeviceCalibration
        {
            get
            {
                foreach (var item in ServiceManager.GetInstance().DeviceServices)
                {
                    if (item is DeviceCalibration deviceCalibration && deviceCalibration.Code == Config.BindDeviceCode)
                        return deviceCalibration;
                }
                return null;
            }
        }

        public ViewCamera View { get; set; }
        public MQTTCamera DeviceService { get; set; }
        public MQTTTerminalCamera Service { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }

        public RelayCommand FetchLatestTemperatureCommand { get; set; }
        public RelayCommand DisPlaySaveCommand { get; set; }


        public DeviceCamera(SysDeviceModel sysResourceModel, MQTTTerminalCamera cameraService) : base(sysResourceModel)
        {
            Service = cameraService;
            DeviceService = new MQTTCamera(Config);

            View = new ViewCamera(this);
            View.View.Title = $"相机视图 - {Config.Code}";
            this.SetIconResource("DrawingImageCamera", View.View);

            EditCommand = new RelayCommand(a =>
            {
                EditCamera window = new EditCamera(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });

            TemplateControl.GetInstance().LoadModCabParam(CalibrationParams, SysResourceModel.Id, ModMasterType.Calibration);

            FetchLatestTemperatureCommand =  new RelayCommand(a => FetchLatestTemperature(a));

            UploadLincenseCommand = new RelayCommand(a => UploadLincense());
            RefreshLincenseCommand = new RelayCommand(a => RefreshLincense());
            DisPlaySaveCommand = new RelayCommand(a => SaveDis());
            DisplayCameraControlLazy = new Lazy<DisplayCameraControl>(() => new DisplayCameraControl(this));

            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());
            OpenPhyCameraMangerCommand = new RelayCommand(a => OpenPhyCameraManger());
        }
        public RelayCommand OpenPhyCameraMangerCommand { get; set; }
        public void OpenPhyCameraManger()
        {
            DeviceService.GetAllCameraID();
            PhyCameraManagerWindow phyCameraManager = new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() };
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


        #region License
        public RelayCommand UploadLincenseCommand { get; set; }
        public RelayCommand RefreshLincenseCommand { get; set; }

        public CameraLicenseDao CameraLicenseDao { get; set; } = new CameraLicenseDao();
        public ObservableCollection<CameraLicenseModel> LicenseModels { get; set; } = new ObservableCollection<CameraLicenseModel>();

        public void RefreshLincense()
        {
            LicenseModels.Clear();
            foreach (var item in CameraLicenseDao.GetAllByPid(SysResourceModel.Id))
            {
                LicenseModels.Add(item);
            };
        }

        private void UploadLincense()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            openFileDialog.Multiselect = true; // 允许多选
            openFileDialog.Filter = "All files (*.*)|*.zip;*.lic"; // 可以设置特定的文件类型过滤器
            openFileDialog.Title = "请选择许可证文件";
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] selectedFiles = openFileDialog.FileNames;

                foreach (string file in selectedFiles)
                {

                    if (Path.GetExtension(file) == ".zip")
                    {
                        try
                        {
                            using ZipArchive archive = ZipFile.OpenRead(file);
                            var licFiles = archive.Entries.Where(entry => Path.GetExtension(entry.FullName).Equals(".lic", StringComparison.OrdinalIgnoreCase)).ToList();

                            foreach (var item in licFiles)
                            {
                                CameraLicenseModel cameraLicenseModel = new CameraLicenseModel();
                                cameraLicenseModel.DevCameraId = SysResourceModel.Id;
                                cameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(item.FullName);

                                using var stream = item.Open();
                                using var reader = new StreamReader(stream, Encoding.UTF8); // 假设文件编码为UTF-8
                                cameraLicenseModel.LicenseValue = reader.ReadToEnd();

                                cameraLicenseModel.CusTomerName = cameraLicenseModel.ColorVisionLincense.Licensee;
                                cameraLicenseModel.Model = cameraLicenseModel.ColorVisionLincense.DeviceMode;
                                cameraLicenseModel.ExpiryDate = cameraLicenseModel.ColorVisionLincense.ExpiryDateTime;
                                if (CameraLicenseDao.GetAllByMAC(cameraLicenseModel.MacAddress, SysResourceModel.Id).Count == 0)
                                {
                                    int ret = CameraLicenseDao.Save(cameraLicenseModel);

                                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "添加成功")}", "ColorVision");
                                }
                                else
                                {
                                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} 重复添加", "ColorVision");
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"解压失败 :{ex.Message}", "ColorVision");
                        }
                    }
                    else if (Path.GetExtension(file) == ".lic")
                    {
                        CameraLicenseModel cameraLicenseModel = new CameraLicenseModel();
                        cameraLicenseModel.DevCameraId = SysResourceModel.Id;
                        cameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(openFileDialog.SafeFileName);
                        cameraLicenseModel.LicenseValue = File.ReadAllText(file);
                        cameraLicenseModel.CusTomerName = cameraLicenseModel.ColorVisionLincense.Licensee;
                        cameraLicenseModel.Model = cameraLicenseModel.ColorVisionLincense.DeviceMode;
                        cameraLicenseModel.ExpiryDate = cameraLicenseModel.ColorVisionLincense.ExpiryDateTime;

                        if (CameraLicenseDao.GetAllByMAC(cameraLicenseModel.MacAddress, SysResourceModel.Id).Count == 0)
                        {
                            int ret = CameraLicenseDao.Save(cameraLicenseModel);
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "添加成功")}", "ColorVision");
                        }
                        else
                        {
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} 重复添加", "ColorVision");
                        }
                        RefreshLincense();

                    }
                    else
                    {
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), "不支持的许可文件后缀", "ColorVision");
                    }

                }



            }
        }
        #endregion

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

        public override UserControl GetDeviceControl() => new DeviceCameraControl(this);
        public override UserControl GetDeviceInfo() => new DeviceCameraControl(this, false);
        
        private Lazy<DisplayCameraControl> DisplayCameraControlLazy { get; set; }

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
