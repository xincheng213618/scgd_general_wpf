#pragma warning disable CA1822,CA1826,CA1863,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Types;
using cvColorVision;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{

    public sealed record LumFourColorCalibrationFileItem(string CameraCode, string RelativePath, string FilePath)
    {
        public string DisplayName => $"{CameraCode} / {RelativePath}";
    }

    public class PhyCameraManager:ViewModelBase
    {
        private static PhyCameraManager _instance;
        private static readonly object Locker = new();
        public static PhyCameraManager GetInstance() { lock (Locker) { return _instance ??= new PhyCameraManager(); } }
        public RelayCommand CreateCommand { get; set; }

        public RelayCommand SearchCameraCommand { get; set; }

        public RelayCommand ImportCommand { get; set; }
        public RelayCommand OpenDeviceManagerCommand { get; set; }
        public RelayCommand OpenLicenseManagerCommand { get; set; }

        public PhyCameraManager()
        {
            CreateCommand = new RelayCommand(a => Create());
            SearchCameraCommand = new RelayCommand(a => SearchCameraIds());
            ImportCommand = new RelayCommand(a => Import());

            OpenDeviceManagerCommand = new RelayCommand(a => OpenDeviceManager());
            OpenLicenseManagerCommand = new RelayCommand(a => OpenLicenseManager());

            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => Application.Current.Dispatcher.Invoke(() => LoadPhyCamera());
            LoadPhyCamera();
            PhyCameras.CollectionChanged += (s, e) =>
            {
                RefreshEmptyCamera();
                RefreshCameraSummary();
            };
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
                    Properties.Resources.MvsLogViewerNotFound,
                    Properties.Resources.PhyCamera_MvsLog,
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
                    string.Format(Properties.Resources.MvsLogViewerStartFailed, ex.Message),
                    Properties.Resources.PhyCamera_MvsLog,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    string.Format(Properties.Resources.UnexpectedErrorOccurred, ex.Message),
                    Properties.Resources.PhyCamera_MvsLog,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
                MessageBox.Show(Application.Current.GetActiveWindow(), $"{Properties.Resources.FailedToOpenDeviceManager}: {ex.Message}", Properties.Resources.PhysicalCameraManager);
            }
        }

        public void OpenLicenseManager()
        {
            new LicenseManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        public void RefreshEmptyCamera()
        {
            Count = SysResourceDao.Instance.GetAllType(101).Count(a => string.IsNullOrEmpty(a.Value));
        }


        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count;

        public int OnlineCameraCount => PhyCameras.Count(IsOnline);

        public int AttentionCameraCount => PhyCameras.Count(RequiresAttention);

        public void RefreshCameraSummary()
        {
            OnPropertyChanged(nameof(OnlineCameraCount));
            OnPropertyChanged(nameof(AttentionCameraCount));
        }

        public PhyCamera? GetPhyCamera(string? Code) => PhyCameras.FirstOrDefault(a => a.Code == Code);

        public IReadOnlyList<LumFourColorCalibrationFileItem> GetLumFourColorCalibrationFiles()
        {
            List<LumFourColorCalibrationFileItem> result = new();
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (PhyCamera phyCamera in PhyCameras)
            {
                foreach (CalibrationResource calibrationResource in EnumerateLumFourColorResources(phyCamera))
                {
                    if (!TryResolveCalibrationFilePath(phyCamera, calibrationResource, out string filePath)
                        || !seenPaths.Add(filePath))
                    {
                        continue;
                    }

                    string relativePath = calibrationResource.SysResourceModel.Value ?? Path.GetFileName(filePath);
                    result.Add(new LumFourColorCalibrationFileItem(phyCamera.Code, relativePath, filePath));
                }
            }

            return result;
        }

        public bool TryGetAnyLumFourColorCalibrationFilePath(out string filePath)
        {
            filePath = GetLumFourColorCalibrationFiles().FirstOrDefault()?.FilePath ?? string.Empty;
            return !string.IsNullOrWhiteSpace(filePath);
        }

        private static IEnumerable<CalibrationResource> EnumerateLumFourColorResources(PhyCamera phyCamera)
        {
            foreach (GroupResource groupResource in EnumerateGroupResources(phyCamera))
            {
                groupResource.SetCalibrationResource();
                if (groupResource.LumFourColor?.IsValid ?? false)
                {
                    yield return groupResource.LumFourColor;
                }
            }

            foreach (CalibrationResource resource in phyCamera.VisualChildren
                .OfType<CalibrationResource>()
                .Where(resource => resource.SysResourceModel.Type == (int)ServiceTypes.LumFourColor && resource.IsValid))
            {
                yield return resource;
            }
        }

        private static IEnumerable<GroupResource> EnumerateGroupResources(PhyCamera phyCamera)
        {
            foreach (GroupResource groupResource in phyCamera.VisualChildren.OfType<GroupResource>())
            {
                yield return groupResource;

                foreach (GroupResource childGroup in EnumerateGroupResources(groupResource))
                {
                    yield return childGroup;
                }
            }
        }

        private static IEnumerable<GroupResource> EnumerateGroupResources(GroupResource groupResource)
        {
            foreach (GroupResource childGroup in groupResource.VisualChildren.OfType<GroupResource>())
            {
                yield return childGroup;

                foreach (GroupResource nestedGroup in EnumerateGroupResources(childGroup))
                {
                    yield return nestedGroup;
                }
            }
        }

        private static bool TryResolveCalibrationFilePath(PhyCamera phyCamera, CalibrationResource calibrationResource, out string filePath)
        {
            filePath = string.Empty;

            if (!Directory.Exists(phyCamera.Config.FileServerCfg.FileBasePath))
            {
                return false;
            }

            string? relativePath = calibrationResource.SysResourceModel.Value;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            filePath = Path.Combine(phyCamera.Config.FileServerCfg.FileBasePath, phyCamera.Code, "cfg", relativePath);
            return File.Exists(filePath);
        }

        private bool _isSearchingCameras;

        public async void SearchCameraIds()
        {
            if (_isSearchingCameras)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.SearchingOnlineCameras, Properties.Resources.PhysicalCameraManager);
                return;
            }

            var searchTypeWindow = new CameraSearchTypeWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (searchTypeWindow.ShowDialog() != true)
            {
                return;
            }

            CameraModel[] selectedCameraModels = searchTypeWindow.SelectedCameraModels;
            if (selectedCameraModels.Length == 0)
            {
                return;
            }

            _isSearchingCameras = true;
            try
            {
                var summary = await Task.Run(() => cvCameraCSLib.SearchCameraIds(selectedCameraModels));
                LoadPhyCamera();
                MarkDiscoveredCamerasOnline(summary);

                var resultWindow = new CameraSearchResultWindow(new CameraSearchResultViewModel(summary, this))
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                resultWindow.ShowDialog();
                RefreshEmptyCamera();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.SearchOnlineCamerasFailed, ex.Message), Properties.Resources.PhysicalCameraManager, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSearchingCameras = false;
            }
        }

        private void MarkDiscoveredCamerasOnline(cvCameraCSLib.CameraDiscoverySummary summary)
        {
            var existingCameras = PhyCameras
                .Where(a => !string.IsNullOrWhiteSpace(a.Code))
                .GroupBy(a => a.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(a => a.Key, a => a.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var camera in summary.Cameras)
            {
                if (existingCameras.TryGetValue(camera.MD5Id, out var phyCamera))
                {
                    phyCamera.SysResourceModel.Remark = "Online";
                }
            }
        }

        public void Create()
        {
            if (!SysResourceDao.Instance.UseLocal && !SysResourceDao.Instance.GetAllType(101).Any(a => string.IsNullOrEmpty(a.Value)))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.NoUncreatedCameraFound, Properties.Resources.PhysicalCameraManager);
                SearchCameraIds();
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
            PhysicalCameraCreationBatch creationBatch = new();
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = true,
                Filter = "All files (*.*)|*.zip;*.lic",
                Title = Properties.Resources.SelectLicenseFilePrompt,
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
                            ProcessZipFile(file, licenses, creationBatch);
                        }
                        else if (Path.GetExtension(file).Equals(".lic", StringComparison.OrdinalIgnoreCase))
                        {
                            ProcessLicFile(file, licenses, creationBatch);
                        }
                        else
                        {
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.UnsupportedLicenseFileExtension, Properties.Resources.LicenseImport);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), string.Format(Properties.Resources.FileProcessFailed, ex.Message), Properties.Resources.LicenseImport);
                    }
                }
            }
            LoadPhyCamera();
            _ = creationBatch.ActivateAsync();
        }

        private void ProcessZipFile(string file, List<LicenseModel> licenses, PhysicalCameraCreationBatch creationBatch)
        {
            using ZipArchive archive = ZipFile.OpenRead(file);
            var licFiles = archive.Entries.Where(entry => Path.GetExtension(entry.FullName).Equals(".lic", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var item in licFiles)
            {
                var licenseModel = GetOrCreateLicenseModel(Path.GetFileNameWithoutExtension(item.FullName), licenses);
                using var stream = item.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                licenseModel.LicenseValue = reader.ReadToEnd();

                UpdateLicenseModel(licenseModel, creationBatch);
            }
        }

        private void ProcessLicFile(string file, List<LicenseModel> licenses, PhysicalCameraCreationBatch creationBatch)
        {
            var licenseModel = GetOrCreateLicenseModel(Path.GetFileNameWithoutExtension(file), licenses);
            licenseModel.LicenseValue = File.ReadAllText(file);

            UpdateLicenseModel(licenseModel, creationBatch);
        }

        internal static LicenseModel GetOrCreateLicenseModel(string macAddress, List<LicenseModel> licenses)
        {
            var licenseModel = licenses.Find(a => string.Equals(a.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase));
            if (licenseModel == null)
            {
                licenseModel = new LicenseModel { MacAddress = macAddress };
                licenses.Add(licenseModel);
            }
            return licenseModel;
        }

        private void UpdateLicenseModel(LicenseModel licenseModel, PhysicalCameraCreationBatch creationBatch)
        {
            licenseModel.CusTomerName = licenseModel.ColorVisionLicense.Licensee;
            licenseModel.Model = licenseModel.ColorVisionLicense.DeviceMode;
            licenseModel.ExpiryDate = licenseModel.ColorVisionLicense.ExpiryDateTime;

            int ret = PhyLicenseDao.Instance.Save(licenseModel);
            if (ret == -1)
            {
                return;
            }

            var phyCamera = PhyCameras.FirstOrDefault(a => string.Equals(a.Code, licenseModel.MacAddress, StringComparison.OrdinalIgnoreCase));
            if (phyCamera != null)
            {
                phyCamera.CameraLicenseModel = licenseModel;
            }

            UpdateSysResource(licenseModel, creationBatch);
        }

        private void UpdateSysResource(LicenseModel licenseModel, PhysicalCameraCreationBatch creationBatch)
        {
            var sysDictionaryModel = FindPhysicalCameraResource(SysResourceDao.Instance.GetAll(), licenseModel.MacAddress);
            if (!RequiresPhysicalCameraCreation(sysDictionaryModel))
            {
                return;
            }

            sysDictionaryModel ??= new SysResourceModel
            {
                Code = licenseModel.MacAddress,
                Type = (int)ServiceTypes.PhyCamera
            };
            ConfigPhyCamera config = new() { Code = sysDictionaryModel.Code, CameraID = sysDictionaryModel.Name ?? string.Empty };
            EnsurePhysicalCameraDirectory(config.FileServerCfg.FileBasePath, sysDictionaryModel.Code);
            sysDictionaryModel.Value = JsonConvert.SerializeObject(config);

            int ret = SysResourceDao.Instance.Save(sysDictionaryModel);
            CompletePhysicalCameraCreation(true, sysDictionaryModel, ret, creationBatch);
            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{licenseModel.MacAddress} {(ret == -1 ? Properties.Resources.AddPhysicalCameraFailed : Properties.Resources.AddPhysicalCameraSuccess)}", Properties.Resources.PhysicalCameraManager);
        }

        internal static SysResourceModel? FindPhysicalCameraResource(IEnumerable<SysResourceModel> resources, string? cameraCode) =>
            resources.FirstOrDefault(a => a.Type == (int)ServiceTypes.PhyCamera && string.Equals(a.Code, cameraCode, StringComparison.OrdinalIgnoreCase));

        internal static bool RequiresPhysicalCameraCreation(SysResourceModel? resource) =>
            resource == null || string.IsNullOrWhiteSpace(resource.Value);

        internal static void EnsurePhysicalCameraDirectory(string? basePath, string? cameraCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(cameraCode);
            if (cameraCode is "." or ".." || cameraCode.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Camera code must be a single directory name.", nameof(cameraCode));
            }

            // Idempotent: creating an existing directory must preserve every calibration file.
            Directory.CreateDirectory(Path.Combine(basePath, cameraCode, "cfg"));
        }

        internal void CompletePhysicalCameraCreation(bool requiresCreation, SysResourceModel resource, int saveResult, PhysicalCameraCreationBatch creationBatch)
        {
            if (saveResult <= 0 || string.IsNullOrWhiteSpace(resource.Code)) return;
            // Legacy PhysicalCamera_Load deletes the camera directory; keep this refresh local.
            LoadPhyCamera();
            creationBatch.RecordSaved(requiresCreation, resource, saveResult);
            if (requiresCreation && PhyCameras.Count == 1)
            {
                string cameraID = resource.Code;
                LicenseModel license = PhyLicenseDao.Instance.GetByMAC(cameraID);
                if (license == null)
                    license = new LicenseModel();
                license.LiceType = 0;
                license.MacAddress = cameraID;
                PhyLicenseDao.Instance.Save(license);

                GetPhyCamera(cameraID).CameraLicenseModel = license;
                PhysicalCameraInitialBinding.BindDevices(requiresCreation, PhyCameras.Count, resource,
                    GetPhyCamera(cameraID).Config, ServiceManager.GetInstance().DeviceServices.ToArray());
            }

        }
        public EventHandler? Loaded { get; set; }

        public ObservableCollection<PhyCamera> PhyCameras { get; set; } = new ObservableCollection<PhyCamera>();

        public void LoadPhyCamera()
        {
            var phyCameraBackup = PhyCameras.ToDictionary(pc => pc.Id, pc => pc);

            var list = SysResourceDao.Instance.GetAllType((int)ServiceTypes.PhyCamera);
            foreach (var stale in PhyCameras.Where(camera => !list.Any(row => row.Id == camera.Id && !string.IsNullOrWhiteSpace(row.Value))).ToArray()) PhyCameras.Remove(stale);
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
                        LoadPhyCameraResources(newPhyCamera);
                        PhyCameras.Add(newPhyCamera);
                    }
                }
            }

            Loaded?.Invoke(this, EventArgs.Empty);
            RefreshCameraSummary();
        }

        private static bool IsOnline(PhyCamera camera)
        {
            return string.Equals(camera.SysResourceModel?.Remark, "Online", StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresAttention(PhyCamera camera)
        {
            return !IsOnline(camera) || camera.HasLicenseAlert || camera.VisualChildren.Count == 0;
        }

        private static void LoadPhyCameraResources(PhyCamera phyCamera)
        {
            var sysResourceModels = SysResourceDao.Instance.GetAllByPid(phyCamera.SysResourceModel.Id).Where(it => !it.IsDelete && it.IsEnable).ToList();
            foreach (var sysResourceModel in sysResourceModels)
            {
                switch (sysResourceModel.Type)
                {
                    case (int)ServiceTypes.Group:
                        var groupResource = new GroupResource(sysResourceModel);
                        phyCamera.AddChild(groupResource);
                        LoadGroupResource(groupResource);
                        break;
                    case int resourceType when CalibrationSlotDefinitions.IsCalibrationType(resourceType):
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
                    case int resourceType when CalibrationSlotDefinitions.IsCalibrationType(resourceType):
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
