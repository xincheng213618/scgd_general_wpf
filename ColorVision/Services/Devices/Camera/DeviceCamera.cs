using ColorVision.MVVM;
using ColorVision.MySql.Service;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.Camera.Views;
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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Devices.Camera
{

    public class ColorVisionVCalibratioItem
    {
        public CalibrationType CalibrationType { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
    }

    public class DeviceCamera : DeviceService<ConfigCamera>
    {
        public MQTTCamera DeviceService { get; set; }

        /// <summary>
        /// 矫正参数
        /// </summary>
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; } = new ObservableCollection<TemplateModel<CalibrationParam>>();

        readonly Lazy<CameraDisplayControl> CameraDisplayControlLazy;  
        readonly Lazy<EditCamera> EditCameraLazy;

        public CameraDisplayControl CameraDisplayControl { get; set; }

        public EditCamera EditCamera { get; set; }

        public ViewCamera View { get; set; }

        public MQTTTerminalCamera Service { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }
        public RelayCommand FetchLatestTemperatureCommand { get; set; }


        public DeviceCamera(SysResourceModel sysResourceModel, MQTTTerminalCamera cameraService) : base(sysResourceModel)
        {
            Service = cameraService;
            DeviceService = new MQTTCamera(Config, Service);
            this.Config.SendTopic = Service.SendTopic;
            this.Config.SubscribeTopic = Service.SubscribeTopic;

            View = new ViewCamera(this);
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


            CalibrationRsourceService.GetInstance().Refresh();
            TemplateControl.GetInstance().LoadModCabParam(CalibrationParams, SysResourceModel.Id, ModMasterType.Calibration);

            FetchLatestTemperatureCommand =  new RelayCommand(a => FetchLatestTemperature(a));

        }
        public CameraTempDao cameraTempDao { get; set; } = new CameraTempDao();   

        private void FetchLatestTemperature(object a)
        {
            var model = cameraTempDao.GetLatestCameraTemp(SysResourceModel.Id);
            if (model != null)
            {
                MessageBox.Show(Application.Current.MainWindow, $"{model.CreateDate:HH:mm:ss} {Environment.NewLine}温度:{model.TempValue}");
            }
            else
            {
                MessageBox.Show(Application.Current.MainWindow, "查询不到对应的温度数据");
            }
        }

        public static void ExtractToDirectoryWithOverwrite(string zipPath, string extractPath)
        {
            // 确保解压目录存在
            Directory.CreateDirectory(extractPath);

            // 打开ZIP文件
            using (ZipArchive archive = ZipFile.Open(zipPath,ZipArchiveMode.Read, Encoding.GetEncoding("gbk")))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // 获取在目标路径中的完整路径
                    string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                    // 确保文件不会解压到目录外面去
                    if (!destinationPath.StartsWith(Path.GetFullPath(extractPath), StringComparison.Ordinal))
                    {
                        throw new IOException("试图解压缩到目录外的文件.");
                    }

                    // 如果文件已存在，删除它
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }
                    else if (!Directory.Exists(Path.GetDirectoryName(destinationPath)))
                    {
                        if (Path.GetDirectoryName(destinationPath) is string die)
                            Directory.CreateDirectory(die);
                    }

                    // 解压缩文件
                    entry.ExtractToFile(destinationPath);
                }
            }
        }

        public void UploadCalibration(object sender)
        {
            UploadWindow uploadwindow = new UploadWindow("校正文件(*.zip, *.cvcal)|*.zip;*.cvcal") { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {

                if (s is Upload upload)
                {
                    string path = upload.UploadFilePath;
                    Task.Run(()=> UploadData(path));
                }
            };
            uploadwindow.ShowDialog();
        }

        public async void UploadData(string UploadFilePath)
        {
            await Task.Delay(100);
            if (File.Exists(UploadFilePath))
            {
                string path = SolutionManager.GetInstance().CurrentSolution.FullName + "\\Cache\\Cal";
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);

                ExtractToDirectoryWithOverwrite(UploadFilePath, path);

                string Cameracfg = path + "\\Camera.cfg";

                string Calibrationcfg = path + "\\Calibration.cfg";
                Dictionary<string, List<ColorVisionVCalibratioItem>> keyValuePairs1 = JsonConvert.DeserializeObject<Dictionary<string, List<ColorVisionVCalibratioItem>>>(File.ReadAllText(Calibrationcfg, Encoding.GetEncoding("gbk")));


                if (keyValuePairs1 != null)
                    foreach (var item in keyValuePairs1)
                    {
                        foreach (var item1 in item.Value)
                        {
                            MsgRecord msgRecord;
                            switch (item1.CalibrationType)
                            {
                                case CalibrationType.DarkNoise:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DarkNoise\\" + item1.FileName, (int)ResouceType.DarkNoise);
                                    break;
                                case CalibrationType.DefectWPoint:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName, (int)ResouceType.DefectPoint);
                                    break;
                                case CalibrationType.DefectBPoint:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName, (int)ResouceType.DefectPoint);
                                    break;
                                case CalibrationType.DefectPoint:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName, (int)ResouceType.DefectPoint);
                                    break;
                                case CalibrationType.DSNU:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DSNU\\" + item1.FileName, (int)ResouceType.DSNU);
                                    break;
                                case CalibrationType.Uniformity:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "Uniformity\\" + item1.FileName, (int)ResouceType.Uniformity);
                                    break;
                                case CalibrationType.Luminance:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "Luminance\\" + item1.FileName, (int)ResouceType.Luminance);
                                    break;
                                case CalibrationType.LumOneColor:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "LumOneColor\\" + item1.FileName, (int)ResouceType.LumOneColor);
                                    break;
                                case CalibrationType.LumFourColor:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "LumFourColor\\" + item1.FileName, (int)ResouceType.LumFourColor);
                                    break;
                                case CalibrationType.LumMultiColor:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "LumMultiColor\\" + item1.FileName, (int)ResouceType.LumMultiColor);
                                    break;
                                case CalibrationType.LumColor:
                                    break;
                                case CalibrationType.Distortion:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "Distortion\\" + item1.FileName, (int)ResouceType.Distortion);
                                    break;
                                case CalibrationType.ColorShift:
                                    msgRecord = DeviceService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "ColorShift\\" + item1.FileName, (int)ResouceType.ColorShift);
                                    break;
                                case CalibrationType.Empty_Num:
                                    break;
                                default:
                                    break;
                            }
                            await Task.Delay(1000);
                        }
                    }

                string CalibrationFile = path + "\\" + "Calibration";
                DirectoryInfo directoryInfo = new DirectoryInfo(CalibrationFile);
                foreach (var item2 in directoryInfo.GetFiles())
                {
                    try
                    {
                        List<ColorVisionVCalibratioItem> keyValuePairs = JsonConvert.DeserializeObject<List<ColorVisionVCalibratioItem>>(File.ReadAllText(item2.FullName, Encoding.GetEncoding("gbk")));
                        if (keyValuePairs != null)
                            if (!Config.CalibrationRsourcesGroups.ContainsKey(Path.GetFileNameWithoutExtension(item2.FullName)))
                            {
                                CalibrationRsourcesGroup calibrationRsourcesGroup = new CalibrationRsourcesGroup() { Title = Path.GetFileNameWithoutExtension(item2.FullName) };
                                foreach (var item1 in keyValuePairs)
                                {
                                    switch (item1.CalibrationType)
                                    {
                                        case CalibrationType.DarkNoise:
                                            calibrationRsourcesGroup.DarkNoise = item1.FileName;
                                            break;
                                        case CalibrationType.DefectWPoint:
                                            calibrationRsourcesGroup.DefectPoint = item1.FileName;
                                            break;
                                        case CalibrationType.DefectBPoint:
                                            calibrationRsourcesGroup.DefectPoint = item1.FileName;
                                            break;
                                        case CalibrationType.DefectPoint:
                                            calibrationRsourcesGroup.DefectPoint = item1.FileName;
                                            break;
                                        case CalibrationType.DSNU:
                                            calibrationRsourcesGroup.DSNU = item1.FileName;
                                            break;
                                        case CalibrationType.Uniformity:
                                            calibrationRsourcesGroup.Uniformity = item1.FileName;
                                            break;
                                        case CalibrationType.Luminance:
                                            calibrationRsourcesGroup.Luminance = item1.FileName;
                                            break;
                                        case CalibrationType.LumOneColor:
                                            calibrationRsourcesGroup.LumOneColor = item1.FileName;
                                            break;
                                        case CalibrationType.LumFourColor:
                                            calibrationRsourcesGroup.LumFourColor = item1.FileName;
                                            break;
                                        case CalibrationType.LumMultiColor:
                                            calibrationRsourcesGroup.LumMultiColor = item1.FileName;
                                            break;
                                        case CalibrationType.LumColor:
                                            calibrationRsourcesGroup.Luminance = item1.FileName;
                                            break;
                                        case CalibrationType.Distortion:
                                            calibrationRsourcesGroup.Distortion = item1.FileName;
                                            break;
                                        case CalibrationType.ColorShift:
                                            calibrationRsourcesGroup.ColorShift = item1.FileName;
                                            break;
                                        case CalibrationType.Empty_Num:
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                Config.CalibrationRsourcesGroups.Add(Path.GetFileName(item2.FullName), calibrationRsourcesGroup);
                            }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }


                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("上传成功");
                    Save();
                });
            }

        }



        public override void Dispose()
        {
            Service.Dispose();
            DeviceService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public override UserControl GetDeviceControl() => new DeviceCameraControl(this);
        public override UserControl GetDeviceInfo() => new DeviceCameraControl(this, false);

        public override UserControl GetDisplayControl() => CameraDisplayControlLazy.Value;
        public override UserControl GetEditControl() => EditCameraLazy.Value;

    }
}
