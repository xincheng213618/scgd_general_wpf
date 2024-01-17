using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
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
using System.Text;
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


            CalibrationRsourceService.GetInstance().Refresh();
            TemplateControl.GetInstance().LoadModCabParam(CalibrationParams, SysResourceModel.Id, ModMasterType.Calibration);
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
                        // 确保文件的目录存在
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
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
                    if (File.Exists(upload.UploadFilePath))
                    {


                        string path = SolutionManager.GetInstance().CurrentSolution.FullName + "\\Cache\\Cal";
                        if (Directory.Exists(path))
                            Directory.Delete(path, true);
                        Directory.CreateDirectory(path);

                        ExtractToDirectoryWithOverwrite(upload.UploadFilePath, path);

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
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DarkNoise\\" + item1.FileName, (int)ResouceType.DarkNoise);
                                            break;
                                        case CalibrationType.DefectWPoint:
                                             msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName, (int)ResouceType.DefectPoint);
                                            break;
                                        case CalibrationType.DefectBPoint:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName, (int)ResouceType.DefectPoint);
                                            break;
                                        case CalibrationType.DefectPoint:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName, (int)ResouceType.DefectPoint);
                                            break;
                                        case CalibrationType.DSNU:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "DSNU\\" + item1.FileName, (int)ResouceType.DSNU);
                                            break;
                                        case CalibrationType.Uniformity:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "Uniformity\\" + item1.FileName, (int)ResouceType.Uniformity);
                                            break;
                                        case CalibrationType.Luminance:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "Luminance\\" + item1.FileName, (int)ResouceType.Luminance);
                                            break;
                                        case CalibrationType.LumOneColor:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "LumOneColor\\" + item1.FileName, (int)ResouceType.LumOneColor);
                                            break;
                                        case CalibrationType.LumFourColor:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "LumFourColor\\" + item1.FileName, (int)ResouceType.LumFourColor);
                                            break;
                                        case CalibrationType.LumMultiColor:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "LumMultiColor\\" + item1.FileName, (int)ResouceType.LumMultiColor);
                                            break;
                                        case CalibrationType.LumColor:
                                            break;
                                        case CalibrationType.Distortion:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "Distortion\\" + item1.FileName, (int)ResouceType.Distortion);
                                            break;
                                        case CalibrationType.ColorShift:
                                            msgRecord = DService.UploadCalibrationFile(item1.FileName, path + "\\Calibration\\" + "ColorShift\\" + item1.FileName, (int)ResouceType.ColorShift);
                                            break;
                                        case CalibrationType.Empty_Num:
                                            break;
                                        default:
                                            break;
                                    }
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
                            catch(Exception ex)
                            {
                            }


                        }
                        MessageBox.Show("上传成功");

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
