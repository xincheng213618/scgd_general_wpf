using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Utilities;
using ColorVision.Themes.Controls.Uploads;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class PhyCamera : BaseResource,ITreeViewItem, IUploadMsg, ICalibrationService<BaseResourceObject>, IIcon
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCalibration));

        public ConfigPhyCamera Config { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }
        public RelayCommand CalibrationEditCommand { get; set; }

        public RelayCommand CalibrationTemplateOpenCommand { get; set; }
        public RelayCommand ResourceManagerCommand { get; set; }

        public RelayCommand UploadLincenseCommand { get; set; }
        public RelayCommand RefreshLincenseCommand { get; set; }
        public RelayCommand ResetCommand { get; set; }
        public RelayCommand EditCommand { get; set; }

        public RelayCommand ProductBrochureCommand { get; set; }

        public RelayCommand EditCameraCommand { get; set; }
        public RelayCommand EditCalibrationCommand { get; set; }

        public ImageSource? QRIcon { get => _QRIcon; set { _QRIcon = value; NotifyPropertyChanged(); } }
        private ImageSource? _QRIcon;
        public ImageSource Icon { get => _Icon; set{ _Icon = value; NotifyPropertyChanged(); } }
        private ImageSource _Icon;

        public ContextMenu ContextMenu { get; set; }

        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; } = new ObservableCollection<TemplateModel<CalibrationParam>>();

        public string? Code => SysResourceModel.Code;
        public PhyCamera(SysResourceModel sysResourceModel):base(sysResourceModel)
        {
            this.SetIconResource("DrawingImageCamera");
            
            Config = BaseResourceObjectExtensions.TryDeserializeConfig<ConfigPhyCamera>(SysResourceModel.Value);
            DeleteCommand = new RelayCommand(a => Delete(), a => AccessControl.Check(PermissionMode.Administrator));
            EditCommand = new RelayCommand(a =>
            {
                EditPhyCamera window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            },a => AccessControl.Check(PermissionMode.Administrator));
            ContentInit();

            ResourceManagerCommand = new RelayCommand(a =>
            {
                ResourceManagerWindow resourceManager = new ResourceManagerWindow(this) { Owner = WindowHelpers.GetActiveWindow() };
                resourceManager.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                resourceManager.ShowDialog();
            });

            UploadCalibrationCommand = new RelayCommand(a => UploadCalibration(a));

            CalibrationParam.LoadResourceParams(CalibrationParams, SysResourceModel.Id);

            ResetCommand = new RelayCommand(a => Reset(), a => AccessControl.Check(PermissionMode.Administrator));

            CalibrationEditCommand = new RelayCommand(a =>
            {
                CalibrationEdit CalibrationEdit = new CalibrationEdit(this);
                CalibrationEdit.Show();
            });

            UploadLincenseCommand = new RelayCommand(a => UploadLincense());
            RefreshLincenseCommand = new RelayCommand(a => RefreshLincense());
            RefreshLincense();
            EditCameraCommand = new RelayCommand(a => DeviceCamera?.EditCommand.Execute(this) ,a=> DeviceCamera!=null && DeviceCamera.EditCommand.CanExecute(this));
            EditCalibrationCommand = new RelayCommand(a => DeviceCalibration?.EditCommand.Execute(this), a => DeviceCalibration != null && DeviceCalibration.EditCommand.CanExecute(this));
            QRIcon = QRCodeHelper.GetQRCode("http://m.color-vision.com/sys-pd/1.html");

            Name = Code ?? string.Empty;

            CalibrationTemplateOpenCommand = new RelayCommand(CalibrationTemplateOpen);

            ProductBrochureCommand = new RelayCommand( a=> OpenProductBrochure(),a=> HaveProductBrochure());
        }
        private string productBrochure;

        private bool HaveProductBrochure()
        {
            if (CameraLicenseModel == null) return false;
            if (CameraLicenseModel.Model == null) return false;
            var model = CameraLicenseModel.Model;
            if (model.Contains("BV", StringComparison.OrdinalIgnoreCase))
            {
                if (model.Contains("2000", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-BV-2000.pdf";
                    return true;
                }
                if (model.Contains("2600", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-BV-2600.pdf";
                    return true;
                }
                if (model.Contains("6100", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-BV-6100.pdf";
                    return true;
                }
            }
            if (model.Contains("LV", StringComparison.OrdinalIgnoreCase))
            {
                if (model.Contains("2000", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-LV-2000.pdf";
                    return true;
                }
                if (model.Contains("2600", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-LV-2600.pdf";
                    return true;
                }
                if (model.Contains("6100", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-LV-6100.pdf";
                    return true;
                }
            }
            if (model.Contains("CV", StringComparison.OrdinalIgnoreCase))
            {
                if (model.Contains("2000", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-CV-2000.pdf";
                    return true;
                }
                if (model.Contains("2600", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-CV-2600.pdf";
                    return true;
                }
                if (model.Contains("6100", StringComparison.OrdinalIgnoreCase))
                {
                    productBrochure = @"Assets\Catalog\Catalog-CV-6100.pdf";
                    return true;
                }
            }
            return false;
        }


        private void OpenProductBrochure()
        {
            PlatformHelper.Open(productBrochure);
        }

        private void CalibrationTemplateOpen(object sender)
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show("数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            var ITemplate = new TemplateCalibrationParam(this);
            new TemplateEditorWindow(ITemplate) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }



        public void Reset()
        {
            if (MessageBox.Show(Application.Current.GetActiveWindow(),"是否清除数据库相关项","ColorVision",MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
            {
                CalibrationParams.Clear();
                this.VisualChildren.Clear();
                Task.Run(() =>
                {
                    SysResourceDao.Instance.DeleteAllByPid(Id ,false);
                    foreach (var item in ModMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "res_pid", Id } }))
                    {
                        ModDetailDao.Instance.DeleteAllByPid(item.Id, false);
                    }
                    ModMasterDao.Instance.DeleteAllByParam(new Dictionary<string, object>() { { "res_pid", Id } }, false);
                });
            }
        }

        public DeviceCamera? DeviceCamera { get; set; }

        public void ReleaseDeviceCamera()
        {
            DeviceCamera = null;
            if (CameraLicenseModel != null)
            {
                CameraLicenseModel.DevCameraId = null;
                CameraLicenseDao.Instance.Save(CameraLicenseModel);
                RefreshLincense();
            }
        }

        public void SetDeviceCamera(DeviceCamera deviceCamera)
        {
            DeviceCamera = deviceCamera;
            if (CameraLicenseModel != null)
            {
                CameraLicenseModel.DevCameraId = deviceCamera.SysResourceModel.Id;
                CameraLicenseDao.Instance.Save(CameraLicenseModel);
                RefreshLincense();

                if (CameraLicenseModel.DevCaliId != null)
                {
                    ServiceManager.GetInstance().DeviceServices.Where(a => a.SysResourceModel.Id == CameraLicenseModel.DevCaliId).ToList().ForEach(a =>
                    {
                        if (a is DeviceCalibration deviceCalibration)
                        {
                            deviceCalibration.RestartRCService();
                        }
                    });
                }
            }
        }
        public DeviceCalibration? DeviceCalibration { get; set; }

        public void ReleaseCalibration() 
        {
            DeviceCalibration = null;

            if (CameraLicenseModel != null)
            {
                CameraLicenseModel.DevCaliId = null;
                CameraLicenseDao.Instance.Save(CameraLicenseModel);
                RefreshLincense();
            }
        }



        public void SetCalibration(DeviceCalibration deviceCalibration)
        {
            DeviceCalibration = deviceCalibration;
            if (CameraLicenseModel != null)
            {
                CameraLicenseModel.DevCaliId = deviceCalibration.SysResourceModel.Id;
                CameraLicenseDao.Instance.Save(CameraLicenseModel);
                RefreshLincense();
                if (CameraLicenseModel.DevCameraId != null)
                {
                    ServiceManager.GetInstance().DeviceServices.Where(a => a.SysResourceModel.Id == CameraLicenseModel.DevCameraId).ToList().ForEach(a =>
                    {
                        if (a is DeviceCamera deviceCamera)
                        {
                            deviceCamera.RestartRCService();
                        }
                    });
                }
            }
        }

        #region License

        public CameraLicenseModel? CameraLicenseModel { get => _CameraLicenseModel; set { _CameraLicenseModel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsLincensed)); } }
        private CameraLicenseModel? _CameraLicenseModel;

        public bool IsLincensed { get => CameraLicenseModel != null;  }

        public void RefreshLincense()
        {
            if (SysResourceModel.Code == null)
            {
                return;
            }
            CameraLicenseModel = CameraLicenseDao.Instance.GetByMAC(SysResourceModel.Code);
        }

        private void UploadLincense()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            openFileDialog.Multiselect = true; // 允许多选
            openFileDialog.Filter = "All files (*.*)|*.zip;*.lic"; // 可以设置特定的文件类型过滤器
            openFileDialog.Title = "请选择许可证文件 " + SysResourceModel.Code;
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
                                CameraLicenseModel cameraLicenseModel = new();
                                cameraLicenseModel.DevCameraId = SysResourceModel.Id;
                                cameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(item.FullName);
                                using var stream = item.Open();
                                using var reader = new StreamReader(stream, Encoding.UTF8); // 假设文件编码为UTF-8
                                cameraLicenseModel.LicenseValue = reader.ReadToEnd();

                                cameraLicenseModel.CusTomerName = cameraLicenseModel.ColorVisionLincense.Licensee;
                                cameraLicenseModel.Model = cameraLicenseModel.ColorVisionLincense.DeviceMode;
                                cameraLicenseModel.ExpiryDate = cameraLicenseModel.ColorVisionLincense.ExpiryDateTime;

                                int ret = CameraLicenseDao.Instance.Save(cameraLicenseModel);
                                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "添加成功")}", "ColorVision");
                            }

                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"解压失败 :{ex.Message}", "ColorVision");
                        }
                    }
                    else if (Path.GetExtension(file) == ".lic")
                    {
                        string Code = Path.GetFileNameWithoutExtension(openFileDialog.SafeFileName);
                        if (Code == SysResourceModel.Code)
                        {
                            CameraLicenseModel = CameraLicenseDao.Instance.GetByMAC(SysResourceModel.Code);
                            if (CameraLicenseModel == null)
                                CameraLicenseModel = new CameraLicenseModel();
                            CameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(openFileDialog.SafeFileName);
                            CameraLicenseModel.LicenseValue = File.ReadAllText(file);
                            CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLincense.Licensee;
                            CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLincense.DeviceMode;
                            CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLincense.ExpiryDateTime;

                            int ret = CameraLicenseDao.Instance.Save(CameraLicenseModel);
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "更新成功")}", "ColorVision");
                            RefreshLincense();
                        }
                        else
                        {
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), "该相机不支持此许可证", "ColorVision");
                        }
                    }
                    else
                    {
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), "不支持的许可文件后缀", "ColorVision");
                    }
                }
            }


            DeviceCalibration?.RestartRCService();
            DeviceCamera?.RestartRCService();
        }
        #endregion


        public static bool ExtractToDirectoryWithOverwrite(string zipPath, string extractPath)
        {
            Directory.CreateDirectory(extractPath);
            try
            {
                using ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Read);
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
                return true;
            }
            catch
            {
                return false;
            }
        }


        public void UploadCalibration(object sender)
        {
            UploadList.Clear();
            UploadWindow uploadwindow = new("校正文件(*.zip, *.cvcal)|*.zip;*.cvcal") { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                UploadMsg uploadMsg = new(this);
                uploadMsg.Show();
                string uploadfilepath = e.UploadFilePath;
                Task.Run(() => UploadData(uploadfilepath));
            };
            uploadwindow.ShowDialog();
        }

        public string Msg { get => _Msg; set { _Msg = value; Application.Current.Dispatcher.Invoke(() => NotifyPropertyChanged()); } }
        private string _Msg;

        public event EventHandler UploadClosed;
        public ObservableCollection<FileUploadInfo> UploadList { get; set; } = new ObservableCollection<FileUploadInfo>();

        public async void UploadData(string UploadFilePath)
        {
            Msg = "正在解压文件：" + " 请稍后...";
            await Task.Delay(10);
            if (File.Exists(UploadFilePath))
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\ColorVision\\Cache";
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);
                await Task.Delay(10);
                Msg = "正在解析校正文件：" + " 请稍后...";
                bool sss = ExtractToDirectoryWithOverwrite(UploadFilePath, path);
                if (!sss)
                {
                    Msg = "解压失败";
                    await Task.Delay(100);
                    Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
                    return;
                }

                try
                {
                    string Cameracfg = path + "\\Camera.cfg";
                    string Calibrationcfg = path + "\\Calibration.cfg";

                    Dictionary<string, List<ZipCalibrationItem>> AllCalFiles = JsonConvert.DeserializeObject<Dictionary<string, List<ZipCalibrationItem>>>(File.ReadAllText(Calibrationcfg, Encoding.GetEncoding("gbk")));

                    Dictionary<string, CalibrationResource> keyValuePairs2 = new();

                    if (AllCalFiles != null)
                    {
                        foreach (var item in AllCalFiles)
                        {
                            foreach (var item1 in item.Value)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    UploadList.Add(new FileUploadInfo() { FileName = item1.Title });
                                });
                            }
                        }

                        foreach (var item in AllCalFiles)
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
                                FileUploadInfo uploadMeta = UploadList.First(a => a.FileName == item1.Title);
                                uploadMeta.FilePath = FilePath;
                                uploadMeta.FileSize = MemorySize.MemorySizeText(MemorySize.FileSize(FilePath));
                                uploadMeta.UploadStatus = UploadStatus.CheckingMD5;
                                await Task.Delay(1);
                                string md5 = Tool.CalculateMD5(FilePath);
                                if (string.IsNullOrWhiteSpace(md5))
                                    continue;

                                bool isExist = false;

                                foreach (var item2 in VisualChildren)
                                {
                                    if (item2 is CalibrationResource CalibrationResource)
                                    {
                                        if (CalibrationResource.SysResourceModel.Code != null && CalibrationResource.SysResourceModel.Code.Contains(md5))
                                        {
                                            keyValuePairs2.TryAdd(item1.Title, CalibrationResource);
                                            isExist = true;
                                            continue;
                                        }
                                    }
                                }
                                if (isExist)
                                {
                                    uploadMeta.UploadStatus = UploadStatus.Completed;
                                    await Task.Delay(10);
                                    continue;
                                }
                                uploadMeta.UploadStatus = UploadStatus.Uploading;
                                Msg = "正在上传校正文件：" + item1.Title + " 请稍后...";
                                await Task.Delay(10);
                                msgRecord = await RCFileUpload.GetInstance().UploadCalibrationFileAsync(SysResourceModel.Code ?? Name, item1.Title, FilePath);
                                if (msgRecord != null && msgRecord.MsgRecordState == MsgRecordState.Success)
                                {
                                    uploadMeta.UploadStatus = UploadStatus.Completed;
                                    string FileName = msgRecord.MsgReturn.Data.FileName;

                                    SysResourceModel sysResourceModel = new();
                                    sysResourceModel.Name = item1.Title;
                                    sysResourceModel.Code = Id + md5;
                                    sysResourceModel.Type = (int)item1.CalibrationType.ToResouceType();
                                    sysResourceModel.Pid = SysResourceModel.Id;
                                    sysResourceModel.Value = Path.GetFileName(FileName);
                                    sysResourceModel.CreateDate = DateTime.Now;
                                    sysResourceModel.Remark = item1.ToJsonN(new JsonSerializerSettings());
                                    int ret = SysResourceDao.Instance.Save(sysResourceModel);
                                    if (sysResourceModel != null)
                                    {
                                        CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            AddChild(calibrationResource);
                                        });
                                        keyValuePairs2.TryAdd(item1.Title, calibrationResource);
                                    }
                                }
                                else
                                {
                                    uploadMeta.UploadStatus = UploadStatus.Failed;
                                }

                            }
                        }

                    }


                    string CalibrationFile = path + "\\" + "Calibration";
                    DirectoryInfo directoryInfo = new(CalibrationFile);
                    foreach (var item2 in directoryInfo.GetFiles())
                    {
                        try
                        {
                            ZipCalibrationGroup zipCalibrationGroup;
                            try
                            {
                                zipCalibrationGroup = new ZipCalibrationGroup();
                                zipCalibrationGroup.List = JsonConvert.DeserializeObject<List<ZipCalibrationItem>>(File.ReadAllText(item2.FullName, Encoding.GetEncoding("gbk"))) ?? new List<ZipCalibrationItem>();
                            }
                            catch (Exception ex)
                            {
                                log.Warn(ex);
                                zipCalibrationGroup = JsonConvert.DeserializeObject<ZipCalibrationGroup>(File.ReadAllText(item2.FullName, Encoding.GetEncoding("gbk")));
                            }

                            if (zipCalibrationGroup != null)
                            {
                                string filePath = Path.GetFileNameWithoutExtension(item2.FullName);

                                bool IsExist = false;
                                foreach (var item in VisualChildren)
                                {
                                    if (item is GroupResource groupResource1 && groupResource1.Name == filePath)
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
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    GroupResource groupResource = GroupResource.AddGroupResource(this, filePath);
                                    if (groupResource != null)
                                    {
                                        foreach (var item1 in zipCalibrationGroup.List)
                                        {
                                            if (keyValuePairs2.TryGetValue(item1.Title, out var colorVisionVCalibratioItems))
                                            {
                                                SysResourceDao.Instance.ADDGroup(groupResource.SysResourceModel.Id, colorVisionVCalibratioItems.SysResourceModel.Id);
                                                groupResource.AddChild(colorVisionVCalibratioItems);
                                            }
                                        }
                                        groupResource.SetCalibrationResource();
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
                            });
                        }
                    }
                    Msg = "上传结束";
                    await Task.Delay(500);
                    SoundPlayerHelper.PlayEmbeddedResource($"/ColorVision.Engine;component/Assets/Sounds/success.wav");
                    Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                    Msg = "找不到配置文件";
                    await Task.Delay(200);
                    SoundPlayerHelper.PlayEmbeddedResource($"/ColorVision.Engine;component/Assets/Sounds/error.wav");
                    Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
                    return;
                }
            }
        }


        public UserControl GetDeviceInfo() => new InfoPhyCamera(this);

        public override void Delete()
        {
            SysResourceModel.Value = null;
            SysResourceDao.Instance.Save(SysResourceModel);
            PhyCameraManager.GetInstance().PhyCameras.Remove(this);
        }

        public void ContentInit()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Edit, Command = EditCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Delete, Command = DeleteCommand });
        }

        public event EventHandler<ConfigPhyCamera> ConfigChanged;

        public void SaveConfig()
        {
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            SysResourceDao.Instance.Save(SysResourceModel);

            ConfigChanged?.Invoke(this, Config);
        }
        public override void Save()
        {
            base.Save();
            SaveConfig();
        }

    }
}
