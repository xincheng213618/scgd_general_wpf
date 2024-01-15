using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using ColorVision.Services.Device.Camera.Calibrations;
using ColorVision.Services.Device.Camera.Configs;
using ColorVision.Services.Device.Camera.Views;
using ColorVision.Services.Msg;
using ColorVision.Solution;
using ColorVision.Templates;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Device.Camera
{

    public class ColorVisionVCalibratioItem
    {
        public CalibrationType CalibrationType { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
    }

    public class DeviceCamera : BaseDevice<ConfigCamera>
    {
        public DeviceServiceCamera DService { get; set; }

        /// <summary>
        /// 矫正参数
        /// </summary>
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; } = new ObservableCollection<TemplateModel<CalibrationParam>>();

        readonly Lazy<CameraDisplayControl> CameraDisplayControlLazy;
        readonly Lazy<EditCamera> EditCameraLazy;

        public CameraDisplayControl CameraDisplayControl { get; set; }

        public EditCamera EditCamera { get; set; }

        public ViewCamera View { get; set; }

        public ServiceCamera Service { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }

        public DeviceCamera(SysResourceModel sysResourceModel, ServiceCamera cameraService) : base(sysResourceModel)
        {
            Service = cameraService;
            DService = new DeviceServiceCamera(Config, Service);
            this.Config.SendTopic = Service.SendTopic;
            this.Config.SubscribeTopic = Service.SubscribeTopic;

            View = new ViewCamera(DService);
            if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage drawingImage)
                Icon = drawingImage;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage drawingImage)
                    Icon = drawingImage;
                View.View.Icon = Icon;
            };
            View.View.Title = "相机视图";
            View.View.Icon = Icon;

            CameraDisplayControlLazy = new Lazy<CameraDisplayControl>(() => CameraDisplayControl ?? new CameraDisplayControl(this));
            EditCameraLazy = new Lazy<EditCamera>(() => EditCamera ?? new EditCamera(this));

            UploadCalibrationCommand = new RelayCommand(a => UploadCalibration(a));
        }
        
        public void UploadCalibration(object sender)
        {
            UploadWindow uploadwindow = new UploadWindow("校正文件(*.zip, *.cvcal)|*.zip;*.cvcal") { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                if (s is Upload upload)
                {
                    if (File.Exists(upload.UploadFilePath))
                    {


                        string path = SolutionManager.GetInstance().CurrentSolution.FullName + "\\Cache\\Cal";
                        if (Directory.Exists(path))
                            Directory.Delete(path, true);
                        Directory.CreateDirectory(path);

                        ZipFile.ExtractToDirectory(upload.UploadFilePath, path);

                        string Cameracfg = path + "\\Camera.cfg";

                        string Calibrationcfg = path + "\\Calibration.cfg";
                        Dictionary<string, List<ColorVisionVCalibratioItem>> keyValuePairs = JsonConvert.DeserializeObject<Dictionary<string, List<ColorVisionVCalibratioItem>>>(File.ReadAllText(Calibrationcfg));

                        if (keyValuePairs != null)
                            foreach (var item in keyValuePairs)
                                if (!Config.Calibration.ContainsKey(item.Key))
                                    Config.Calibration.Add(item.Key, item.Value);


                        string cachepath = path + "\\Calibration";
                        if (Directory.Exists(cachepath))
                            Directory.Delete(cachepath,true);
                        Directory.CreateDirectory(cachepath);

                        string CalibrationFile = path + "\\" + "Calibration.zip";
                        ZipFile.ExtractToDirectory(CalibrationFile, cachepath);
                        DirectoryInfo directoryInfo = new DirectoryInfo(cachepath);
                        foreach (var item in directoryInfo.GetFiles())
                        {
                            MsgRecord msgRecord = DService.UploadCalibrationFile(Path.GetFileNameWithoutExtension(item.FullName), item.FullName, 1001);
                        }
                        MessageBox.Show("上传中");

                        Save();
                    }
                }
            };
            uploadwindow.ShowDialog();
        }



        public override void Dispose()
        {
            Service.Dispose();
            DService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public override UserControl GetDeviceControl() => new DeviceCameraControl(this);
        public override UserControl GetDeviceInfo() => new DeviceCameraControl(this, false);

        public override UserControl GetDisplayControl() => CameraDisplayControlLazy.Value;
        public override UserControl GetEditControl() => EditCameraLazy.Value;

    }
}
