using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Extension;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.Themes.Controls.Uploads;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras
{

    public enum LicenseState
    {
        Unlicensed,
        Licensed,
        Expired,
        Invalid
    }

    public class PhyCamera : ServiceBase,ITreeViewItem, IUploadMsg
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PhyCamera));

        public ConfigPhyCamera Config { get; set; }

        [CommandDisplay("UploadCalibrationFiles", Order =100)]
        public RelayCommand UploadCalibrationCommand { get; set; }
        [CommandDisplay("CalibrationEditCommand", Order =102)]
        public RelayCommand CalibrationEditCommand { get; set; }
        [CommandDisplay("CaliTemplateSet",Order =101)]
        public RelayCommand CalibrationTemplateOpenCommand { get; set; }
        public RelayCommand UploadLicenseCommand { get; set; }
        [CommandDisplay("DownLicOnline")]
        public RelayCommand UploadLicenseNetCommand { get; set; }

        public RelayCommand RefreshLicenseCommand { get; set; }
        [CommandDisplay("CopyLicense")]
        public RelayCommand CopyLicenseCommand { get; set; }
        [CommandDisplay("ExportLicense")]
        public RelayCommand ExportLicenseCommand { get; set; }

        [CommandDisplay("Reset", CommandType = CommandType.Highlighted,Order = 9999)]
        public RelayCommand ResetCommand { get; set; }
        [CommandDisplay("ModifyConfiguration", Order = -99)]
        public RelayCommand EditCommand { get; set; }
        public RelayCommand CopyConfigCommand { get; set; }
        [CommandDisplay("OpenConfigFile")]
        public RelayCommand OpenSettingDirectoryCommand { get; set; }

        [CommandDisplay("CreateRestorePoint", Order = 99999)]
        public RelayCommand CreatResotreCommand { get; set; }

        [CommandDisplay("LoadRestorePoint", Order = 99999)]
        public RelayCommand LoadResotreCommand { get; set; }

        [CommandDisplay("EditFilterWheelConfig", Order = 103)]
        public RelayCommand FilterWheelEditCommand { get; set; }

        public ContextMenu ContextMenu { get; set; }

        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; } = new ObservableCollection<TemplateModel<CalibrationParam>>();

        public string Code => SysResourceModel.Code ?? string.Empty;


        public PhyCamera(SysResourceModel sysResourceModel):base(sysResourceModel)
        {            
            Config = ServiceObjectBaseExtensions.TryDeserializeConfig<ConfigPhyCamera>(SysResourceModel.Value);
            DeleteCommand = new RelayCommand(a => Delete(), a => AccessControl.Check(PermissionMode.Administrator));
            EditCommand = new RelayCommand(a =>
            {
                EditPhyCamera window = new EditPhyCamera(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            },a => AccessControl.Check(PermissionMode.Administrator));


            CopyConfigCommand = new RelayCommand(a => Common.NativeMethods.Clipboard.SetText(Config.ToJsonN()));
            ContentInit();

            UploadCalibrationCommand = new RelayCommand(a => UploadCalibration(a));

            CalibrationParam.LoadResourceParams(CalibrationParams, SysResourceModel.Id);

            ResetCommand = new RelayCommand(a => Reset(), a => AccessControl.Check(PermissionMode.Administrator));

            CalibrationEditCommand = new RelayCommand(a =>
            {
                CalibrationEdit CalibrationEdit = new CalibrationEdit(this);
                CalibrationEdit.Show();
            });

            UploadLicenseCommand = new RelayCommand(a => UploadLicense());
            RefreshLicenseCommand = new RelayCommand(a => RefreshLicense());
            CopyLicenseCommand = new RelayCommand(a => CopyLicense(), a => CameraLicenseModel != null && !string.IsNullOrEmpty(CameraLicenseModel.LicenseValue));
            ExportLicenseCommand = new RelayCommand(a => ExportLicense(), a => CameraLicenseModel != null && !string.IsNullOrEmpty(CameraLicenseModel.LicenseValue));
            RefreshLicense();

            Name = Code ?? string.Empty;

            CalibrationTemplateOpenCommand = new RelayCommand(CalibrationTemplateOpen);

            UploadLicenseNetCommand = new RelayCommand(a => Task.Run(() => UploadLicenseNet()),a=> AccessControl.Check(PermissionMode.SuperAdministrator));
            OpenSettingDirectoryCommand = new RelayCommand(a => OpenSettingDirectory(),a=> Directory.Exists(Path.Combine(Config.FileServerCfg.FileBasePath, Code)));
            CreatResotreCommand = new RelayCommand(a => CreateRestore());
            LoadResotreCommand = new RelayCommand(a => LoadResotre());

            FilterWheelEditCommand = new RelayCommand(a =>
            {
                EditFilterWheelConfig window = new EditFilterWheelConfig(Config.FilterWheelConfig);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (window.ShowDialog() == true)
                {
                    SaveConfig();
                }
            }, a => AccessControl.Check(PermissionMode.Administrator));

        }

        bool IsCreateRestore ;
        public async void CreateRestore()
        {
            if (IsCreateRestore)
            {
                MessageBox.Show("IsCreateRestore");
                return;
            }
            IsCreateRestore = true;
            // 1. 设置最终保存路径 (.cvcal 文件路径)
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string restoreDir = Path.Combine(desktopPath, "Restore");
            if (!Directory.Exists(restoreDir))
            {
                Directory.CreateDirectory(restoreDir);
            }
            // 最终文件：Desktop\Restore\{Code}.cvcal
            string finalZipPath = Path.Combine(restoreDir, $"{Code}.cvcal");

            // 2. 创建临时目录用于构建文件结构
            // 使用 GUID 防止临时文件夹重名冲突
            string tempRootPath = Path.Combine(Path.GetTempPath(), "ColorVisionTemp", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRootPath);

            try
            {
                // --- 原有逻辑开始 (路径改为 tempRootPath) ---

                // 注意：原逻辑是在文件夹下又建了一层 Code，通常作为单文件格式，建议直接把内容放在根目录下
                // 如果你坚持要有一层文件夹，可以保留下面这行，否则建议直接用 tempRootPath
                // string workingPath = Path.Combine(tempRootPath, Code); 
                string workingPath = tempRootPath; // 这里我选择直接放在压缩包根目录，这样打开更清爽

                if (!Directory.Exists(workingPath)) Directory.CreateDirectory(workingPath);

                // 保存相机配置
                string cameraConfigPath = Path.Combine(workingPath, "CameraConfig.cfg");
                Config.ToJsonNFile(cameraConfigPath);

                // 保存 License
                if (CameraLicenseModel != null)
                {
                    string licPath = Path.Combine(workingPath, $"{Code}.lic");
                    File.WriteAllText(licPath, CameraLicenseModel.LicenseValue);
                }

                // 创建 Calibration 文件夹
                string calibrationPath = Path.Combine(workingPath, "Calibration");
                if (!Directory.Exists(calibrationPath))
                {
                    Directory.CreateDirectory(calibrationPath);
                }

                Dictionary<string, List<ZipCalibrationItem>> keyValuePairs = new Dictionary<string, List<ZipCalibrationItem>>();
                List<ZipCalibrationItem> calibrationItems = new List<ZipCalibrationItem>();
                keyValuePairs.Add("Calibration", calibrationItems);

                // 遍历 VisualChildren
                foreach (var item in VisualChildren)
                {
                    if (item is CalibrationResource calibrationResource)
                    {
                        ZipCalibrationItem zipCalibrationItem = new ZipCalibrationItem();
                        zipCalibrationItem.CalibrationType = ((ServiceTypes)calibrationResource.SysResourceModel.Type).ToCalibrationType();
                        zipCalibrationItem.Title = calibrationResource.Config.Title;
                        zipCalibrationItem.FileName = calibrationResource.Config.FileName;
                        calibrationItems.Add(zipCalibrationItem);

                        var serviceType = (ServiceTypes)calibrationResource.SysResourceModel.Type;

                        // 查找父级 PhyCamera 并复制文件
                        if (calibrationResource.GetAncestor<PhyCamera>() is PhyCamera phyCamera)
                        {
                            if (Directory.Exists(phyCamera.Config.FileServerCfg.FileBasePath))
                            {
                                string path = calibrationResource.SysResourceModel.Value ?? string.Empty;
                                string filepath = Path.Combine(phyCamera.Config.FileServerCfg.FileBasePath, phyCamera.Code, "cfg", path);

                                // 确保源文件存在
                                if (File.Exists(filepath))
                                {
                                    // 在临时目录中建立分类文件夹
                                    string typeDir = Path.Combine(calibrationPath, serviceType.ToString());
                                    if (!Directory.Exists(typeDir))
                                        Directory.CreateDirectory(typeDir);

                                    string entryPath = Path.Combine(typeDir, calibrationResource.Config.FileName);
                                    File.Copy(filepath, entryPath, true);
                                }
                            }
                        }
                    }

                    if (item is GroupResource groupResource)
                    {
                        List<ZipCalibrationItem> zipCalibrationItems = new List<ZipCalibrationItem>();
                        foreach (var cc in groupResource.VisualChildren)
                        {
                            if (cc is CalibrationResource caesource)
                            {
                                ZipCalibrationItem zipCalibrationItem = new ZipCalibrationItem();
                                zipCalibrationItem.CalibrationType = ((ServiceTypes)caesource.SysResourceModel.Type).ToCalibrationType();
                                zipCalibrationItem.Title = caesource.Config.Title;
                                zipCalibrationItem.FileName = caesource.Config.FileName;
                                zipCalibrationItems.Add(zipCalibrationItem);
                            }
                        }

                        // 序列化 Group JSON
                        string json = JsonConvert.SerializeObject(zipCalibrationItems, Formatting.Indented);
                        string groupPath = Path.Combine(calibrationPath, $"{groupResource.Name}.cfg");
                        File.WriteAllText(groupPath, json);
                    }
                }

                // 保存主索引 JSON
                string mainJson = JsonConvert.SerializeObject(keyValuePairs, Formatting.Indented);
                string mainJsonPath = Path.Combine(workingPath, "Calibration.cfg");
                File.WriteAllText(mainJsonPath, mainJson);

                // --- 原有逻辑结束 ---

                // 3. 打包压缩为 .cvcal

                // 如果目标文件已存在，先删除
                if (File.Exists(finalZipPath))
                {
                    File.Delete(finalZipPath);
                }

                await Task.Run(async () => 
                {
                    // 核心压缩代码
                    ZipFile.CreateFromDirectory(workingPath, finalZipPath, CompressionLevel.NoCompression, false);
                }
                );



                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.RestorePointCreatedSuccessfully);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建 Restore 文件失败: {ex.Message}");
            }
            finally
            {
                // 4. 清理临时文件
                if (Directory.Exists(tempRootPath))
                {
                    try
                    {
                        Directory.Delete(tempRootPath, true);
                    }
                    catch
                    {
                        // 忽略清理临时文件时的错误，不影响主流程
                    }
                }
                IsCreateRestore = false;
            }
        }

        public void LoadResotre()
        {
            try
            {
                string ResotrePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                ResotrePath = Path.Combine(ResotrePath, "Restore", Code);

                string CameraConfigPath = Path.Combine(ResotrePath, "CameraConfig.cfg");

                ConfigPhyCamera configPhyCamera = JsonConvert.DeserializeObject<ConfigPhyCamera>(File.ReadAllText(CameraConfigPath));

                configPhyCamera.CopyTo(Config);
                string LicPath = Path.Combine(ResotrePath, $"{Code}.lic");
                if (File.Exists(LicPath))
                {
                    CameraLicenseModel = PhyLicenseDao.Instance.GetByMAC(Code);
                    if (CameraLicenseModel == null)
                        CameraLicenseModel = new LicenseModel();
                    CameraLicenseModel.LiceType = 0;
                    CameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(LicPath);
                    CameraLicenseModel.LicenseValue = File.ReadAllText(LicPath);
                    CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLicense.Licensee;
                    CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLicense.DeviceMode;
                    CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLicense.ExpiryDateTime;
                    int ret = PhyLicenseDao.Instance.Save(CameraLicenseModel);
                    if (ret == 1)
                    {
                        RefreshLicense();
                    }
                }
                SaveConfig();
                MessageBox.Show(Application.Current.GetActiveWindow(),"还原成功");
            }
            catch(Exception ex)
            {
                log.Error(ex);
            }


        }

        public void OpenSettingDirectory()
        {
            if (Directory.Exists(Path.Combine(Config.FileServerCfg.FileBasePath, Code)))
            {
                Common.Utilities.PlatformHelper.OpenFolder(Path.Combine(Config.FileServerCfg.FileBasePath, Code));
            }

        }

        public async Task UploadLicenseNet()
        {
            // 设置请求的URL和数据
            string url = "https://color-vision.picp.net/license/api/v1/license/onlyDownloadLicense";
            var postData = new { macSn = Code };
            string DirLicense = $"{Environments.DirAppData}\\Licenses";
            if (!Directory.Exists(DirLicense))
                Directory.CreateDirectory(DirLicense);

            string fileName = $"{DirLicense}\\{Code}-license.zip";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // 发送POST请求
                    HttpResponseMessage response = await client.PostAsJsonAsync(url, postData);
                    // 检查响应状态码
                    response.EnsureSuccessStatusCode();

                    // 确保返回的是一个文件而不是JSON
                    if (response.Content.Headers.ContentType?.MediaType == "application/json")
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                    }
                    // 获取文件名
                    fileName = "license.zip"; // 默认文件名
                    if (response.Content.Headers.ContentDisposition != null)
                    {
                        fileName = response.Content.Headers.ContentDisposition.FileName?.Trim('"');
                    }
                    fileName = $"{DirLicense}\\{fileName}";
                    using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                    SetLicense(fileName);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

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
                    using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, });
                    Db.Deleteable<SysResourceModel>().Where(x => x.Pid == SysResourceModel.Id).ExecuteCommand();
                    var ModMasterModels = Db.Queryable<ModMasterModel>().Where(x => x.ResourceId == Id).ToList();
                    foreach (var item in ModMasterModels)
                    {
                        Db.Deleteable<ModDetailModel>().Where(x => x.Pid == item.Id).ExecuteCommand();
                    }
                    Db.Deleteable<ModMasterModel>().Where(x => x.ResourceId == Id).ExecuteCommand();
                });
            }
        }

        public override void Delete()
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.ConfirmDelete, "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel) return;
            CalibrationParams.Clear();
            this.VisualChildren.Clear();

            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, });
            Db.Deleteable<SysResourceModel>().Where(x => x.Pid == SysResourceModel.Id).ExecuteCommand();

            var ModMasterModels = Db.Queryable<ModMasterModel>().Where(x => x.ResourceId == Id).ToList();
            foreach (var item in ModMasterModels)
            {
                Db.Deleteable<ModDetailModel>().Where(x => x.Pid == item.Id).ExecuteCommand();
            }
            Db.Deleteable<ModMasterModel>().Where(x => x.ResourceId == Id).ExecuteCommand();

            SysResourceModel.Value = null;
            SysResourceDao.Instance.Save(SysResourceModel);
            PhyCameraManager.GetInstance().PhyCameras.Remove(this);
        }

        public DeviceCamera? DeviceCamera { get; set; }

        public void ReleaseDeviceCamera()
        {
            DeviceCamera = null;
            if (CameraLicenseModel != null)
            {
                CameraLicenseModel.DevCameraId = null;
                PhyLicenseDao.Instance.Save(CameraLicenseModel);
                RefreshLicense();
            }
        }

        public void SetDeviceCamera(DeviceCamera deviceCamera)
        {
            DeviceCamera = deviceCamera;
            if (CameraLicenseModel != null)
            {

                CameraLicenseModel.DevCameraId = deviceCamera.SysResourceModel.Id;
                PhyLicenseDao.Instance.Save(CameraLicenseModel);
                RefreshLicense();

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

        public void SetCalibration(DeviceCalibration deviceCalibration)
        {
            DeviceCalibration = deviceCalibration;
            if (CameraLicenseModel != null)
            {
                CameraLicenseModel.DevCaliId = deviceCalibration.SysResourceModel.Id;
                PhyLicenseDao.Instance.Save(CameraLicenseModel);
                RefreshLicense();
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

        public LicenseModel? CameraLicenseModel { get => _CameraLicenseModel; set { _CameraLicenseModel = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLicensed)); OnPropertyChanged(nameof(LicenseSolidColorBrush)); OnPropertyChanged(nameof(LicenseExpiryText)); OnPropertyChanged(nameof(LicenseExpiryColor)); } }
        private LicenseModel? _CameraLicenseModel;

        public LicenseState LicenseState { get 
            {
                if (CameraLicenseModel == null || CameraLicenseModel.ColorVisionLicense == null) return LicenseState.Unlicensed;
                if (CameraLicenseModel.ColorVisionLicense.ExpiryDateTime >DateTime.Now) return LicenseState.Licensed;
                if (CameraLicenseModel.ColorVisionLicense.ExpiryDateTime < DateTime.Now) return LicenseState.Expired;
                return LicenseState.Invalid;
            }
        }


        public SolidColorBrush LicenseSolidColorBrush
        {
            get
            {
                switch (LicenseState)
                {
                    case LicenseState.Unlicensed:
                        return new SolidColorBrush(Colors.Red);
                    case LicenseState.Licensed:
                        var expiryDate = CameraLicenseModel.ExpiryDate.Value;
                        if (expiryDate < DateTime.Now)
                            return new SolidColorBrush(Colors.Red);

                        var daysRemaining = (expiryDate - DateTime.Now).Days;
                        if (daysRemaining <= 30)
                            return new SolidColorBrush(Colors.Orange);
                        return new SolidColorBrush(Colors.Green);
                    case LicenseState.Expired:
                        return new SolidColorBrush(Colors.Red);
                    case LicenseState.Invalid:
                        return new SolidColorBrush(Colors.Gray);
                    default:
                        return new SolidColorBrush(Colors.Gray);
                }
            }
        }


        public bool IsLicensed { get => CameraLicenseModel != null;  }

        public string LicenseExpiryText
        {
            get
            {
                if (CameraLicenseModel == null || CameraLicenseModel.ExpiryDate == null)
                    return "无许可证";
                
                var expiryDate = CameraLicenseModel.ExpiryDate.Value;
                if (expiryDate < DateTime.Now)
                    return $"已过期 ({expiryDate:yyyy-MM-dd})";
                
                var daysRemaining = (expiryDate - DateTime.Now).Days;
                if (daysRemaining <= 30)
                    return $"剩余 {daysRemaining} 天 ({expiryDate:yyyy-MM-dd})";
                
                return $"有效期至 {expiryDate:yyyy-MM-dd}";
            }
        }

        public SolidColorBrush LicenseExpiryColor
        {
            get
            {
                if (CameraLicenseModel == null || CameraLicenseModel.ExpiryDate == null)
                    return new SolidColorBrush(Colors.Red);
                
                var expiryDate = CameraLicenseModel.ExpiryDate.Value;
                if (expiryDate < DateTime.Now)
                    return new SolidColorBrush(Colors.Red);
                
                var daysRemaining = (expiryDate - DateTime.Now).Days;
                if (daysRemaining <= 30)
                    return new SolidColorBrush(Colors.Orange);
                
                return new SolidColorBrush(Colors.Green);
            }
        }

        public void RefreshLicense()
        {
            if (SysResourceModel.Code == null)
            {
                return;
            }
            CameraLicenseModel = PhyLicenseDao.Instance.GetByMAC(SysResourceModel.Code);
        }

        private void UploadLicense()
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
                    SetLicense(file);
                }
            }
        }

        public bool SetLicense(string filepath)
        {
            if (!File.Exists(filepath)) return false;
            if (Path.GetExtension(filepath) == ".zip")
            {
                try
                {
                    using ZipArchive archive = ZipFile.OpenRead(filepath);
                    var licFiles = archive.Entries.Where(entry => Path.GetExtension(entry.FullName).Equals(".lic", StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var item in licFiles)
                    {
                        string Code = Path.GetFileNameWithoutExtension(item.FullName);
                        if (Code == SysResourceModel.Code)
                        {
                            CameraLicenseModel = PhyLicenseDao.Instance.GetByMAC(SysResourceModel.Code);
                            if (CameraLicenseModel == null)
                                CameraLicenseModel = new LicenseModel();
                            CameraLicenseModel.LiceType = 0;
                            CameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(item.FullName);
                            using var stream = item.Open();
                            using var reader = new StreamReader(stream, Encoding.UTF8); // 假设文件编码为UTF-8
                            CameraLicenseModel.LicenseValue = reader.ReadToEnd();

                            CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLicense.Licensee;
                            CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLicense.DeviceMode;
                            CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLicense.ExpiryDateTime;

                            int ret = PhyLicenseDao.Instance.Save(CameraLicenseModel);
                            if(ret == 1)
                            {
                                RefreshLicense();
                                DeviceCalibration?.RestartRCService();
                                DeviceCamera?.RestartRCService();
                            }

                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "添加成功")}", "ColorVision");
                            if (ret == -1)
                            {
                                return false;
                            }
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"解压失败 :{ex.Message}", "ColorVision");
                    return false;
                }
            }
            else if (Path.GetExtension(filepath) == ".lic")
            {
                string Code = Path.GetFileNameWithoutExtension(filepath);
                if (Code == SysResourceModel.Code)
                {
                    CameraLicenseModel = PhyLicenseDao.Instance.GetByMAC(SysResourceModel.Code);
                    if (CameraLicenseModel == null)
                        CameraLicenseModel = new LicenseModel();
                    CameraLicenseModel.LiceType = 0;
                    CameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(filepath);
                    CameraLicenseModel.LicenseValue = File.ReadAllText(filepath);
                    CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLicense.Licensee;
                    CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLicense.DeviceMode;
                    CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLicense.ExpiryDateTime;

                    int ret = PhyLicenseDao.Instance.Save(CameraLicenseModel);
                    if (ret == 1)
                    {
                        RefreshLicense();
                        DeviceCalibration?.RestartRCService();
                        DeviceCamera?.RestartRCService();
                    }
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "更新成功")}", "ColorVision");
                    if (ret == -1)
                    {
                        return false;
                    }
                    return true;
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
            return false;
        }

        public void CopyLicense()
        {
            if (CameraLicenseModel == null || string.IsNullOrEmpty(CameraLicenseModel.LicenseValue))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.NoLicenseAvailable, "ColorVision");
                return;
            }

            try
            {
                Common.NativeMethods.Clipboard.SetText(CameraLicenseModel.LicenseValue);
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.LicenseCopiedToClipboard, "ColorVision");
            }
            catch (Exception ex)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{Properties.Resources.CopyLicenseFailed}: {ex.Message}", "ColorVision");
            }
        }

        public void ExportLicense()
        {
            if (CameraLicenseModel == null || string.IsNullOrEmpty(CameraLicenseModel.LicenseValue))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.NoLicenseAvailable, "ColorVision");
                return;
            }

            try
            {
                using var saveFileDialog = new System.Windows.Forms.SaveFileDialog
                {
                    Filter = "License files (*.lic)|*.lic|All files (*.*)|*.*",
                    Title = Properties.Resources.ExportLicenseToFile,
                    FileName = $"{Code}.lic",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    File.WriteAllText(saveFileDialog.FileName, CameraLicenseModel.LicenseValue, Encoding.UTF8);
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.LicenseExportedSuccessfully, "ColorVision");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{Properties.Resources.ExportLicenseFailed}: {ex.Message}", "ColorVision");
            }
        }


        #endregion



        public void UploadCalibration(object sender)
        {
            string DesPath = Path.Combine(Config.FileServerCfg.FileBasePath, Code, "cfg");
            if (!Directory.Exists(DesPath))
            {
                try
                {
                    Directory.CreateDirectory(DesPath);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    MessageBox.Show(ex.Message);
                    return;
                }
            }


            UploadList.Clear();
            UploadWindow uploadwindow = new UploadWindow("校正文件(*.zip, *.cvcal)|*.zip;*.cvcal") { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                UploadMsg uploadMsg = new UploadMsg(this);
                uploadMsg.Show();
                string uploadfilepath = e.UploadFilePath;
                Task.Run(() => UploadData(DesPath, uploadfilepath));
            };
            uploadwindow.ShowDialog();
        }

        public string Msg { get => _Msg; set { _Msg = value; Application.Current.Dispatcher.Invoke(() => OnPropertyChanged()); } }
        private string _Msg;

        public event EventHandler UploadClosed;
        public ObservableCollection<FileUploadInfo> UploadList { get; set; } = new ObservableCollection<FileUploadInfo>();

        public async void UploadData(string DesPath, string UploadFilePath)
        {
            Msg = "正在解压文件：" + " 请稍后...";
            await Task.Delay(10);
            if (File.Exists(UploadFilePath))
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ColorVision\\Cache";
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);
                Msg = "正在解析校正文件：" + " 请稍后...";
                bool sss = ZIPHelper.ExtractToDirectoryWithOverwrite(UploadFilePath, path);
                if (!sss)
                {
                    Msg = "解压失败";
                    MessageBox.Show("解压失败");
                    await Task.Delay(100);
                    Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
                    return;
                }

                try
                {
                    string Calibrationcfg = path + "\\Calibration.cfg";

                    Dictionary<string, List<ZipCalibrationItem>> AllCalFiles = JsonConvert.DeserializeObject<Dictionary<string, List<ZipCalibrationItem>>>(File.ReadAllText(Calibrationcfg, Encoding.GetEncoding("gbk")));

                    Dictionary<string, CalibrationResource> keyValuePairs2 = new();

                    var uniqueItems = AllCalFiles.SelectMany(item => item.Value)
                             .GroupBy(x => new { x.CalibrationType, x.Title })
                             .Select(g => g.First());

                    foreach (var item in uniqueItems)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            UploadList.Add(new FileUploadInfo() { FileName = item.Title });
                        });
                    }

                    foreach (var item in uniqueItems)
                    {
                        string FilePath = string.Empty;
                        switch (item.CalibrationType)
                        {
                            case CalibrationType.DarkNoise:
                                FilePath = path + "\\Calibration\\" + "DarkNoise\\" + item.FileName;
                                break;
                            case CalibrationType.DefectWPoint:
                                FilePath = path + "\\Calibration\\" + "DefectPoint\\" + item.FileName;
                                break;
                            case CalibrationType.DefectBPoint:
                                FilePath = path + "\\Calibration\\" + "DefectPoint\\" + item.FileName;
                                break;
                            case CalibrationType.DefectPoint:
                                FilePath = path + "\\Calibration\\" + "DefectPoint\\" + item.FileName;
                                break;
                            case CalibrationType.DSNU:
                                FilePath = path + "\\Calibration\\" + "DSNU\\" + item.FileName;
                                break;
                            case CalibrationType.Uniformity:
                                FilePath = path + "\\Calibration\\" + "Uniformity\\" + item.FileName;
                                break;
                            case CalibrationType.Luminance:
                                FilePath = path + "\\Calibration\\" + "Luminance\\" + item.FileName;
                                break;
                            case CalibrationType.LumOneColor:
                                FilePath = path + "\\Calibration\\" + "LumOneColor\\" + item.FileName;
                                break;
                            case CalibrationType.LumFourColor:
                                FilePath = path + "\\Calibration\\" + "LumFourColor\\" + item.FileName;
                                break;
                            case CalibrationType.LumMultiColor:
                                FilePath = path + "\\Calibration\\" + "LumMultiColor\\" + item.FileName;
                                break;
                            case CalibrationType.LumColor:
                                break;
                            case CalibrationType.Distortion:
                                FilePath = path + "\\Calibration\\" + "Distortion\\" + item.FileName;
                                break;
                            case CalibrationType.ColorShift:
                                FilePath = path + "\\Calibration\\" + "ColorShift\\" + item.FileName;
                                break;
                            case CalibrationType.ColorDiff:
                                FilePath = path + "\\Calibration\\" + "ColorDiff\\" + item.FileName;
                                break;
                            case CalibrationType.LineArity:
                                FilePath = path + "\\Calibration\\" + "LineArity\\" + item.FileName;
                                break;
                            case CalibrationType.Empty_Num:
                                break;
                            default:
                                break;
                        }

                        FileUploadInfo uploadMeta = UploadList.First(a => a.FileName == item.Title);
                        uploadMeta.FilePath = FilePath;
                        uploadMeta.FileSize = MemorySize.MemorySizeText(MemorySize.FileSize(FilePath));
                        uploadMeta.UploadStatus = UploadStatus.CheckingMD5;
                        await Task.Delay(1);
                        string md5 = Tool.CalculateMD5(FilePath);
                        bool isExist = false;
                        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                        db.Queryable<SysResourceModel>().Where(a => a.Pid == SysResourceModel.Id && a.Name == item.Title && a.Code != null && a.Code.Contains(md5)).ToList().ForEach(a =>
                        {
                            keyValuePairs2.TryAdd(item.Title, CalibrationResource.EnsureInstance(a));
                            isExist = true;
                        });

                        if (isExist)
                        {
                            uploadMeta.UploadStatus = UploadStatus.Completed;
                            continue;
                        }

                        uploadMeta.UploadStatus = UploadStatus.Uploading;
                        Msg = "正在上传校正文件：" + item.Title + " 请稍后...";
                        await Task.Delay(10);

                        try
                        {
                            string FileName = Path.GetFileName(FilePath);
                            string DesFilePath = Path.Combine(DesPath, FileName);
                            File.Copy(FilePath, DesFilePath, true);
                            File.Delete(FilePath);
                            SysResourceModel sysResourceModel = new();
                            sysResourceModel.Name = item.Title;
                            sysResourceModel.Code = Id + md5 + item.Title;
                            sysResourceModel.Type = (int)item.CalibrationType.ToResouceType();
                            sysResourceModel.Pid = SysResourceModel.Id;
                            sysResourceModel.Value = Path.GetFileName(FileName);
                            sysResourceModel.CreateDate = DateTime.Now;
                            sysResourceModel.Remark = item.ToJsonN(new JsonSerializerSettings());
                            int ret = SysResourceDao.Instance.Save(sysResourceModel);

                            if (sysResourceModel != null)
                            {
                                CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    AddChild(calibrationResource);
                                });
                                keyValuePairs2.TryAdd(item.Title, calibrationResource);
                            }
                            uploadMeta.UploadStatus = UploadStatus.Completed;
                        }
                        catch (Exception ex)
                        {
                            uploadMeta.UploadStatus = UploadStatus.Failed;
                            log.Error(ex);
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
                                log.Info("校正组解析失败，使用旧版的解析方案");
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
                                        using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, });

                                        foreach (var item1 in zipCalibrationGroup.List)
                                        {
                                            if (keyValuePairs2.TryGetValue(item1.Title, out var colorVisionVCalibratioItems))
                                            {
                                                Db.Insertable(new SysResourceGoupModel { ResourceId = groupResource.SysResourceModel.Id, GroupId = colorVisionVCalibratioItems.SysResourceModel.Id }).ExecuteCommand();
                                                groupResource.AddChild(colorVisionVCalibratioItems);
                                            }
                                        }
                                        groupResource.SetCalibrationResource();
                                        groupResource.Save();
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
                    if (UploadList.Any(a => a.UploadStatus == UploadStatus.Failed))
                    {
                        SoundPlayerHelper.PlayEmbeddedResource($"/ColorVision.Engine;component/Assets/Sounds/error.wav");
                    }
                    else
                    {
                        await Task.Delay(500);
                        SoundPlayerHelper.PlayEmbeddedResource($"/ColorVision.Engine;component/Assets/Sounds/success.wav");
                        Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
                    }
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                    Msg = ex.Message;
                    SoundPlayerHelper.PlayEmbeddedResource($"/ColorVision.Engine;component/Assets/Sounds/error.wav");

                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
                        UploadClosed.Invoke(this, new EventArgs());
                    } );
                    return;
                }
            }
        }

        public UserControl GetDeviceInfo()
        {
            return new InfoPhyCamera(this);
        }

        public void ContentInit()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Edit, Command = EditCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Delete, Command = DeleteCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuCopy, Command = CopyConfigCommand });
        }

        public event EventHandler<ConfigPhyCamera> ConfigChanged;

        public void SaveConfig()
        {
            //加这个是因为陈宏那边没有解析IsUseCFW，要清除掉ChannelCfgs,以及IsCOM的配置。具体的是因为SDK中串口和滤轮是分离的，所以在配置中需要单独处理
            if (!Config.CFW.IsUseCFW)
            {
                Config.CFW.ChannelCfgs.Clear();
                Config.CFW.IsCOM = false;
            }

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
