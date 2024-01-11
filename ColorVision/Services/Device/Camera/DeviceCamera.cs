using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using ColorVision.Services.Device.Camera;
using ColorVision.Services.Device.Camera.Views;
using ColorVision.Services.Msg;
using ColorVision.Solution;
using ColorVision.Templates;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using System;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Device.Camera
{
    public class DeviceCamera : BaseDevice<ConfigCamera>
    {
        public DeviceServiceCamera DService { get; set; }

        readonly Lazy<CameraDisplayControl> CameraDisplayControlLazy;
        readonly Lazy<EditCamera> EditCameraLazy;

        public CameraDisplayControl CameraDisplayControl { get; set; }
        public EditCamera EditCamera { get; set; }

        public ViewCamera View { get; set; }

        public ServiceCamera Service { get; set; }

        public cvColorVision.TakeImageMode cameraMode { get => _cameraMode; set { _cameraMode = value; IsVideo = value == cvColorVision.TakeImageMode.Live; } }
        private cvColorVision.TakeImageMode _cameraMode;
        public bool IsVideo
        {
            get => _isVideo;
            set
            {
                _isVideo = value; NotifyPropertyChanged();
            }
        }
        private bool _isVideo;
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
                        string path = SolutionManager.GetInstance().CurrentSolution.FullName + "\\Cache";
                        ZipFile.ExtractToDirectory(upload.UploadFilePath, path);
                        string filename = path +"\\" + Path.GetFileNameWithoutExtension(upload.UploadFilePath);
                        string Cameracfg = filename + "\\Camera.cfg";
                        string Calibrationcfg = filename + "\\Calibration.cfg";

                    }


                    MsgRecord msgRecord = DService?.UploadCalibrationFile(Path.GetFileNameWithoutExtension(upload.UploadFileName), upload.UploadFilePath, 1001);
                    msgRecord.MsgRecordStateChanged += (s) =>
                    {



                        MessageBox.Show("sucess");
                    };
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
