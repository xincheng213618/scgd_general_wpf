using ColorVision.Common.Utilities;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.MySql.Service;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.Services.Interfaces;
using ColorVision.Services.Msg;
using ColorVision.Solution;
using ColorVision.Templates;
using ColorVision.Themes;
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

    public class DeviceCamera : DeviceService<ConfigCamera>, IUploadMsg
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlControl));


        public MQTTCamera DeviceService { get; set; }

        /// <summary>
        /// 矫正参数
        /// </summary>
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; } = new ObservableCollection<TemplateModel<CalibrationParam>>();

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

            DisplayLazy = new Lazy<DisplayCameraControl>(() => DisplayCameraControl ?? new DisplayCameraControl(this));
            EditCameraLazy = new Lazy<EditCamera>(() => { EditCamera ??= new EditCamera(this); return EditCamera; });

            UploadCalibrationCommand = new RelayCommand(a => UploadCalibration(a));


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
            using (ZipArchive archive = ZipFile.Open(zipPath,ZipArchiveMode.Read))
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
                    if (entry.Length != 0)
                    {
                        entry.ExtractToFile(destinationPath);
                    }
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
                    UploadMsg uploadMsg = new UploadMsg(this);
                    uploadMsg.Show();
                    string path = upload.UploadFilePath;
                    Task.Run(()=> UploadData(path));
                }
            };
            uploadwindow.ShowDialog();
        }

        public string Msg { get => _Msg; set {  _Msg = value;  Application.Current.Dispatcher.Invoke(() => NotifyPropertyChanged()); } }
        private string _Msg;

        public event EventHandler UploadClosed;

        public async void UploadData(string UploadFilePath)
        {
            Msg = "正在解压文件：" + " 请稍后...";
            await Task.Delay(10);
            if (File.Exists(UploadFilePath))
            {
                Msg ="正在解压文件：" +" 请稍后...";
                string path = SolutionManager.GetInstance().CurrentSolution.FullName + "\\Cache\\Cal";
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);
                await Task.Delay(10);
                Msg = "正在解析校正文件：" + " 请稍后...";
                ExtractToDirectoryWithOverwrite(UploadFilePath, path);

                string Cameracfg = path + "\\Camera.cfg";

                string Calibrationcfg = path + "\\Calibration.cfg";
                Dictionary<string, List<ColorVisionVCalibratioItem>> keyValuePairs1 = JsonConvert.DeserializeObject<Dictionary<string, List<ColorVisionVCalibratioItem>>>(File.ReadAllText(Calibrationcfg, Encoding.GetEncoding("gbk")));

                Dictionary<string, CalibrationResource> keyValuePairs2 = new Dictionary<string, CalibrationResource>();

                if (keyValuePairs1 != null)
                {
                    foreach (var item in keyValuePairs1)
                    {
                        foreach (var item1 in item.Value)
                        {
                            MsgRecord msgRecord = null;
                            string FilePath = string.Empty;
                            switch (item1.CalibrationType)
                            {
                                case CalibrationType.DarkNoise:
                                    FilePath = path + "\\Calibration\\" + "DarkNoise\\" + item1.FileName;
                                    break;
                                case CalibrationType.DefectWPoint:
                                    FilePath = path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName;
                                    break;
                                case CalibrationType.DefectBPoint:
                                    FilePath = path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName;
                                    break;
                                case CalibrationType.DefectPoint:
                                    FilePath = path + "\\Calibration\\" + "DefectPoint\\" + item1.FileName;
                                    break;
                                case CalibrationType.DSNU:
                                    FilePath = path + "\\Calibration\\" + "DSNU\\" + item1.FileName;
                                    break;
                                case CalibrationType.Uniformity:
                                    FilePath = path + "\\Calibration\\" + "Uniformity\\" + item1.FileName;
                                    break;
                                case CalibrationType.Luminance:
                                    FilePath = path + "\\Calibration\\" + "Luminance\\" + item1.FileName;
                                    break;
                                case CalibrationType.LumOneColor:
                                    FilePath = path + "\\Calibration\\" + "LumOneColor\\" + item1.FileName;
                                    break;
                                case CalibrationType.LumFourColor:
                                    FilePath = path + "\\Calibration\\" + "LumFourColor\\" + item1.FileName;
                                    break;
                                case CalibrationType.LumMultiColor:
                                    FilePath = path + "\\Calibration\\" + "LumMultiColor\\" + item1.FileName;
                                    break;
                                case CalibrationType.LumColor:
                                    break;
                                case CalibrationType.Distortion:
                                    FilePath = path + "\\Calibration\\" + "Distortion\\" + item1.FileName;
                                    break;
                                case CalibrationType.ColorShift:
                                    FilePath = path + "\\Calibration\\" + "ColorShift\\" + item1.FileName;
                                    break;
                                case CalibrationType.Empty_Num:
                                    break;
                                default:
                                    break;
                            }
                            string md5 = Tool.CalculateMD5(FilePath);
                            if (string.IsNullOrWhiteSpace(md5))
                                continue;

                            bool isExist = false;
                            foreach (var item2 in VisualChildren)
                            {
                                if (item2 is CalibrationResource CalibrationResource)
                                {
                                    if (CalibrationResource.SysResourceModel.Code == md5)
                                    {
                                        keyValuePairs2.Add(item1.Title, CalibrationResource);
                                        isExist = true;
                                        continue;
                                    }
                                }
                            }
                            if (isExist)
                                continue;


                            Msg ="正在上传校正文件：" + item1.Title + " 请稍后...";
                            await Task.Delay(10);

                            switch (item1.CalibrationType)
                            {
                                case CalibrationType.DarkNoise:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.DarkNoise);
                                    break;
                                case CalibrationType.DefectWPoint:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.DefectPoint);
                                    break;
                                case CalibrationType.DefectBPoint:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.DefectPoint);
                                    break;
                                case CalibrationType.DefectPoint:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.DefectPoint);
                                    break;
                                case CalibrationType.DSNU:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.DSNU);
                                    break;
                                case CalibrationType.Uniformity:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.Uniformity);
                                     break;
                                case CalibrationType.Luminance:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.Luminance);
                                    break;
                                case CalibrationType.LumOneColor:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.LumOneColor);
                                    break;
                                case CalibrationType.LumFourColor:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.LumFourColor);
                                    break;
                                case CalibrationType.LumMultiColor:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.LumMultiColor);
                                    break;
                                case CalibrationType.LumColor:
                                    break;
                                case CalibrationType.Distortion:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.Distortion);
                                    break;
                                case CalibrationType.ColorShift:
                                    msgRecord = await DeviceService.UploadCalibrationFileAsync(item1.Title, FilePath, (int)ResouceType.ColorShift);
                                    break;
                                case CalibrationType.Empty_Num:
                                    break;
                                default:
                                    break;
                            }

                            if (msgRecord != null && msgRecord.MsgRecordState == MsgRecordState.Success)
                            {
                                string FileName = msgRecord.MsgReturn.Data.FileName;
                        
                                SysResourceDao sysResourceDao = new SysResourceDao();
                                SysResourceModel sysResourceModel = new SysResourceModel();
                                sysResourceModel.Name = item1.Title;
                                sysResourceModel.Code = md5;
                                sysResourceModel.Type = (int)item1.CalibrationType.ToResouceType();
                                sysResourceModel.Pid = this.SysResourceModel.Id;
                                sysResourceModel.Value = Path.GetFileName(FileName);
                                sysResourceDao.Save(sysResourceModel);
                                if (sysResourceModel != null)
                                {
                                    CalibrationResource calibrationResource = new CalibrationResource(sysResourceModel);
                                    this.AddChild(calibrationResource);
                                    keyValuePairs2.Add(item1.Title, calibrationResource);
                                }
                            }

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
                        {
                            string filePath = Path.GetFileNameWithoutExtension(item2.FullName);

                            bool IsExist = false;
                            foreach (var item in VisualChildren)
                            {
                                if (item is GroupService groupService1 && groupService1.Name == filePath)
                                {
                                    log.Info($"{filePath} Exit");
                                    IsExist = true;
                                    break;
                                }
                            }
                            if (IsExist)
                            {
                                continue;
                            }
                            GroupService groupService = GroupService.AddGroupService(this, filePath);
                            if (groupService != null)
                            {
                                foreach (var item1 in keyValuePairs)
                                {
                                    if (keyValuePairs2.TryGetValue(item1.Title, out var colorVisionVCalibratioItems))
                                    {
                                        groupService.AddChild(colorVisionVCalibratioItems);
                                    }
                                }
                                groupService.SetCalibrationResource(this);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                Msg = "上传结束";
                await Task.Delay(100);
                Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
            }

        }




        public override UserControl GetDeviceControl() => new DeviceCameraControl(this);
        public override UserControl GetDeviceInfo() => new DeviceCameraControl(this, false);


        readonly Lazy<DisplayCameraControl> DisplayLazy;
        public DisplayCameraControl DisplayCameraControl { get; set; }
        public override UserControl GetDisplayControl() => DisplayLazy.Value;


        readonly Lazy<EditCamera> EditCameraLazy;
        public EditCamera EditCamera { get; set; }
        public override UserControl GetEditControl() => EditCameraLazy.Value;


        public override void Dispose()
        {
            Service.Dispose();
            DeviceService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
