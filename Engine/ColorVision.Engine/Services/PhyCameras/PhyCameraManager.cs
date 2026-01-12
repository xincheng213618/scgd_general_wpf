using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Plugins;
using cvColorVision;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    [FileExtension(".lic")]

    public class LicFileProcess : IFileProcessor
    {
        public int Order => 1;

        public void Export(string filePath)
        {

        }

        public bool Process(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            string content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content)) return false;
            string LicenseValue = Tool.Base64Decode(content);
            ColorVisionLicense colorVisionLicense =  JsonConvert.DeserializeObject<ColorVisionLicense>(LicenseValue);
            if (colorVisionLicense == null) return false;

            if (MessageBox.Show("是否导入许可证" + colorVisionLicense.DeviceMode, "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.No) return false;
            LicenseModel licenseModel = PhyLicenseDao.Instance.GetByMAC(Path.GetFileNameWithoutExtension(filePath)) ?? new LicenseModel();
            licenseModel.LicenseValue = content;
            PhyLicenseDao.Instance.Save(licenseModel);
            return true;
        }
    }

    public class PhyCameraManagerConfig : ViewModelBase, IConfig
    {

    }


    public class PhyCameraManager:ViewModelBase
    {
        private static PhyCameraManager _instance;
        private static readonly object Locker = new();
        public static PhyCameraManager GetInstance() { lock (Locker) { return _instance ??= new PhyCameraManager(); } }
        public static SqlSugar.SqlSugarClient Db => MySqlControl.GetInstance().DB;

        public RelayCommand CreateCommand { get; set; }

        public RelayCommand ImportCommand { get; set; }
        public RelayCommand EditCofigCommand { get; set; }
        public RelayCommand OpenDeviceManagerCommand { get; set; }
        public RelayCommand OpenLicenseManagerCommand { get; set; }

        public PhyCameraManagerConfig Config { get; set; } = ConfigService.Instance.GetRequiredService<PhyCameraManagerConfig>();

        public PhyCameraManager()
        {
            CreateCommand = new RelayCommand(a => Create());
            ImportCommand = new RelayCommand(a => Import());

            EditCofigCommand = new RelayCommand(a => EditCofig());
            OpenDeviceManagerCommand = new RelayCommand(a => OpenDeviceManager());
            OpenLicenseManagerCommand = new RelayCommand(a => OpenLicenseManager());

            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => LoadPhyCamera());
            if (MySqlControl.GetInstance().IsConnect)
                LoadPhyCamera();
            RefreshEmptyCamera();
            PhyCameras.CollectionChanged += (s, e) => RefreshEmptyCamera();
            if(PhyCameras.Count > 0)
            {
                PhyCameras[0].IsSelected = true;
            }

            OpenMVSLogViewerCommand = new RelayCommand(a => OpenMVSLogViewer(), a => File.Exists("C:\\Program Files (x86)\\MVS\\Applications\\Win64\\LogViewer.exe"));
        }

        public RelayCommand OpenMVSLogViewerCommand { get; set; }
        public void OpenMVSLogViewer()
        {
            string MVSLogViewer = "C:\\Program Files (x86)\\MVS\\Applications\\Win64\\LogViewer.exe";
            if (!File.Exists(MVSLogViewer))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "MVS LogViewer not found at the expected location.",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = MVSLogViewer,
                    UseShellExecute = true,
                    Verb = "runas" // 请求管理员权限
                };
                Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 用户取消了 UAC 提示或其他启动失败
                if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                    // 用户取消了权限提升，可以选择不显示错误
                    return;
                }

                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to start MVS LogViewer:  {ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"An unexpected error occurred: {ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void EditCofig()
        {
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.ShowDialog();
        }

        public void OpenDeviceManager()
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "devmgmt.msc",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"{Properties.Resources.FailedToOpenDeviceManager}: {ex.Message}", "ColorVision");
            }
        }

        public void OpenLicenseManager()
        {
            new LicenseManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        public void RefreshEmptyCamera()
        {
            Count = MySqlControl.GetInstance().DB.Queryable<SysResourceModel>().Where(a => a.Type == 101 && SqlFunc.IsNullOrEmpty(a.Value)).Count();
        }


        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count;

        public PhyCamera? GetPhyCamera(string? Code) => PhyCameras.FirstOrDefault(a => a.Code == Code);

        public void Create()
        {
            if (MySqlControl.GetInstance().DB.Queryable<SysResourceModel>().Where(a => a.Type == 101 && SqlFunc.IsNullOrEmpty(a.Value)).Count() <= 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到未创建的相机,请插上相机后在尝试",nameof(PhyCameraManager));
                foreach (var item in ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>())
                {
                    item.RefreshDeviceId();
                }
                return;
            }

            var createWindow = new CreateWindow(this)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            createWindow.ShowDialog();


        }

        public void Import()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = true,
                Filter = "All files (*.*)|*.zip;*.lic",
                Title = "请选择许可证文件",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] selectedFiles = openFileDialog.FileNames;
                var licenses = PhyLicenseDao.Instance.GetAll();

                foreach (string file in selectedFiles)
                {
                    try
                    {
                        if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            ProcessZipFile(file, licenses);
                        }
                        else if (Path.GetExtension(file).Equals(".lic", StringComparison.OrdinalIgnoreCase))
                        {
                            ProcessLicFile(file, licenses);
                        }
                        else
                        {
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), "不支持的许可文件后缀", "ColorVision");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), $"处理文件失败 :{ex.Message}", "ColorVision");
                    }
                }
            }
            LoadPhyCamera();
        }

        private  void ProcessZipFile(string file, List<LicenseModel> licenses)
        {
            using ZipArchive archive = ZipFile.OpenRead(file);
            var licFiles = archive.Entries.Where(entry => Path.GetExtension(entry.FullName).Equals(".lic", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var item in licFiles)
            {
                var licenseModel = GetOrCreateLicenseModel(Path.GetFileNameWithoutExtension(item.FullName), licenses);
                using var stream = item.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                licenseModel.LicenseValue = reader.ReadToEnd();

                UpdateLicenseModel(licenseModel);
            }
        }

        private  void ProcessLicFile(string file, List<LicenseModel> licenses)
        {
            var licenseModel = GetOrCreateLicenseModel(Path.GetFileNameWithoutExtension(file), licenses);
            licenseModel.LicenseValue = File.ReadAllText(file);

            UpdateLicenseModel(licenseModel);
        }

        private static LicenseModel GetOrCreateLicenseModel(string macAddress, List<LicenseModel> licenses)
        {
            var licenseModel = licenses.Find(a => a.MacAddress == macAddress) ?? new LicenseModel { MacAddress = macAddress };
            return licenseModel;
        }

        private void UpdateLicenseModel(LicenseModel licenseModel)
        {
            licenseModel.CusTomerName = licenseModel.ColorVisionLicense.Licensee;
            licenseModel.Model = licenseModel.ColorVisionLicense.DeviceMode;
            licenseModel.ExpiryDate = licenseModel.ColorVisionLicense.ExpiryDateTime;

            int ret = PhyLicenseDao.Instance.Save(licenseModel);

            UpdateSysResource(licenseModel);
        }

        private  void UpdateSysResource(LicenseModel licenseModel)
        {
            var sysDictionaryModel = SysResourceDao.Instance.GetAll().Find(a => a.Code == licenseModel.MacAddress);
            if (sysDictionaryModel == null)
            {
                sysDictionaryModel = new SysResourceModel
                {
                    Code = licenseModel.MacAddress,
                    Type = (int)ServiceTypes.PhyCamera,
                    Value = JsonConvert.SerializeObject(CreateDefaultConfig())
                };

                int ret = SysResourceDao.Instance.Save(sysDictionaryModel);
                if(ret != -1 && sysDictionaryModel.Code !=null)
                {
                    CreatePhysicalCameraFloder(sysDictionaryModel.Code);
                }
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{licenseModel.MacAddress} {(ret == -1 ? "添加物理相机失败" : "添加物理相机成功")}", "ColorVision");
            }
            else
            {
                sysDictionaryModel.Value = JsonConvert.SerializeObject(CreateDefaultConfig());
                int ret= SysResourceDao.Instance.Save(sysDictionaryModel);
                if (ret != -1 && sysDictionaryModel.Code != null)
                {
                    CreatePhysicalCameraFloder(sysDictionaryModel.Code);
                }
            }
        }

        public void CreatePhysicalCameraFloder(string cameraID)
        {
            RCFileUpload.GetInstance().CreatePhysicalCameraFloder(cameraID);
            LoadPhyCamera();
            if (PhyCameras.Count == 1)
            {
                LicenseModel license = PhyLicenseDao.Instance.GetByMAC(cameraID);
                if (license == null)
                    license = new LicenseModel();
                license.LiceType = 0;
                license.MacAddress = cameraID;
                PhyLicenseDao.Instance.Save(license);

                GetPhyCamera(cameraID).CameraLicenseModel = license;


                foreach (var item in ServiceManager.GetInstance().DeviceServices)
                {
                    if (item is DeviceCamera deviceCamera)
                    {
                        deviceCamera.Config.SN = cameraID;
                        deviceCamera.Config.CameraCode = cameraID;
                        deviceCamera.Save();
                    }
                    if (item is DeviceAlgorithm deviceAlgorithm)
                    {
                        deviceAlgorithm.Config.SN = cameraID;
                        deviceAlgorithm.Save();
                    }
                    if (item is DeviceCalibration deviceCalibration)
                    {
                        deviceCalibration.Config.CameraCode = cameraID;
                        deviceCalibration.Save();
                    }
                }
            }

        }

        private static ConfigPhyCamera CreateDefaultConfig()
        {
            return new ConfigPhyCamera
            {
                TakeImageMode = TakeImageMode.Measure_Normal,
                ImageBpp = ImageBpp.bpp8,
                Channel = ImageChannel.One,
            };
        }

        public EventHandler Loaded { get; set; }

        public ObservableCollection<PhyCamera> PhyCameras { get; set; } = new ObservableCollection<PhyCamera>();

        public void LoadPhyCamera()
        {
            var phyCameraBackup = PhyCameras.ToDictionary(pc => pc.Id, pc => pc);

          
            var list = MySqlControl.GetInstance().DB.Queryable<SysResourceModel>().Where(x => x.Type == (int)ServiceTypes.PhyCamera).ToList();
            foreach (var item in list)
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    // 创建新的 PhyCamera 对象

                    // 如果备份字典中存在该 PhyCamera 的 Id
                    if (phyCameraBackup.TryGetValue(item.Id, out var existingPhyCamera))
                    {
                        existingPhyCamera.Name = item.Name ?? string.Empty;
                        existingPhyCamera.SysResourceModel = item;
                        existingPhyCamera.Config.CameraID = item.Name ?? string.Empty;
                    }
                    else
                    {
                        var newPhyCamera = new PhyCamera(item);
                        if (Authorization.Instance.PermissionMode == PermissionMode.SuperAdministrator)
                        {
                            if (newPhyCamera.LicenseState != LicenseState.Licensed)
                                Task.Run(async () =>
                                {
                                    await newPhyCamera.UploadLicenseNet();
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        ServiceManager.GetInstance().DeviceServices.OfType<DeviceAlgorithm>().ToList().ForEach(a => a.Save());
                                    });
                                }
                                );
                        }

                        LoadPhyCameraResources(newPhyCamera);
                        // 添加新的 PhyCamera 对象到集合中
                        PhyCameras.Add(newPhyCamera);
                    }
                }
            }

            Loaded?.Invoke(this, EventArgs.Empty);
        }

        private static void LoadPhyCameraResources(PhyCamera phyCamera)
        {
            var sysResourceModels =  Db.Queryable<SysResourceModel>().Where(it => it.Pid == phyCamera.SysResourceModel.Id && it.IsDelete == false && it.IsEnable == true).ToList();
            foreach (var sysResourceModel in sysResourceModels)
            {
                switch (sysResourceModel.Type)
                {
                    case (int)ServiceTypes.Group:
                        var groupResource = new GroupResource(sysResourceModel);
                        phyCamera.AddChild(groupResource);
                        LoadGroupResource(groupResource);
                        break;
                    case >= 30 and <= 50:
                        var calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                        phyCamera.AddChild(calibrationResource);
                        break;
                    default:
                        var baseFileResource = new ServiceFileBase(sysResourceModel);
                        phyCamera.AddChild(baseFileResource);
                        break;
                }
            }
        }

        public static void LoadGroupResource(GroupResource groupResource)
        {
            var sysResourceModels = SysResourceDao.Instance.GetGroupResourceItems(groupResource.SysResourceModel.Id);
            foreach (var sysResourceModel in sysResourceModels)
            {
                switch (sysResourceModel.Type)
                {
                    case (int)ServiceTypes.Group:
                        var nestedGroupResource = new GroupResource(sysResourceModel);
                        LoadGroupResource(nestedGroupResource);
                        groupResource.AddChild(nestedGroupResource);
                        break;
                    case >= 30 and <= 50:
                        var calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                        groupResource.AddChild(calibrationResource);
                        break;
                    default:
                        var baseResource = new ServiceBase(sysResourceModel);
                        groupResource.AddChild(baseResource);
                        break;
                }
            }
            groupResource.SetCalibrationResource();
        }
    }
}
