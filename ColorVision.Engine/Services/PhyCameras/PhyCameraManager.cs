using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.PhyCameras.Configs;
using ColorVision.Services.PhyCameras.Dao;
using ColorVision.Services.RC;
using ColorVision.Services.Types;
using ColorVision.UserSpace;
using cvColorVision;
using CVCommCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.Services.PhyCameras
{
    public class PhyCameraManager
    {
        private static PhyCameraManager _instance;
        private static readonly object _locker = new();
        public static PhyCameraManager GetInstance() { lock (_locker) { return _instance ??= new PhyCameraManager(); } }
        public RelayCommand CreateCommand { get; set; }
        public RelayCommand ImportCommand { get; set; }

        public PhyCameraManager() 
        {
            CreateCommand = new RelayCommand(a => Create());
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) => LoadPhyCamera();
            if (MySqlControl.GetInstance().IsConnect)
                LoadPhyCamera();

            ImportCommand = new RelayCommand(a => Import());

        }



        public PhyCamera? GetPhyCamera(string CamerID) => PhyCameras.FirstOrDefault(a => a.Name == CamerID);

        public void Create()
        {
            CreateWindow createWindow = new(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            createWindow.ShowDialog();
        }
        public void Import()
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
                var licenses = CameraLicenseDao.Instance.GetAll();

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
                                CameraLicenseModel? cameraLicenseModel = licenses.Find(a => a.MacAddress == Path.GetFileNameWithoutExtension(item.FullName));
                                if (cameraLicenseModel==null)
                                    cameraLicenseModel = new CameraLicenseModel();
                                cameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(item.FullName);
                                using var stream = item.Open();
                                using var reader = new StreamReader(stream, Encoding.UTF8); // 假设文件编码为UTF-8
                                cameraLicenseModel.LicenseValue = reader.ReadToEnd();

                                cameraLicenseModel.CusTomerName = cameraLicenseModel.ColorVisionLincense.Licensee;
                                cameraLicenseModel.Model = cameraLicenseModel.ColorVisionLincense.DeviceMode;
                                cameraLicenseModel.ExpiryDate = cameraLicenseModel.ColorVisionLincense.ExpiryDateTime;

                                int ret = CameraLicenseDao.Instance.Save(cameraLicenseModel);
                                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "添加成功")}", "ColorVision");
                                SysDictionaryModel? sysDictionaryModel = SysDictionaryDao.Instance.GetAll().Find(a => a.Code == cameraLicenseModel.MacAddress);
                                if (sysDictionaryModel == null)
                                {
                                    sysDictionaryModel = new SysDictionaryModel();
                                    sysDictionaryModel.Code = cameraLicenseModel.MacAddress;
                                    sysDictionaryModel.Type = (int)ServiceTypes.PhyCamera;
                                    SysDictionaryDao.Instance.Save(sysDictionaryModel);

                                    SysResourceModel? sysResourceModel = SysResourceDao.Instance.GetByCode(cameraLicenseModel.MacAddress);
                                    if (sysResourceModel == null)
                                        sysResourceModel = new SysResourceModel("", cameraLicenseModel.MacAddress, (int)PhysicalResourceType.PhyCamera, UserConfig.Instance.TenantId);

                                    var CreateConfig = new ConfigPhyCamera
                                    {
                                        CameraType = CameraType.LV_Q,
                                        TakeImageMode = TakeImageMode.Measure_Normal,
                                        ImageBpp = ImageBpp.bpp8,
                                        Channel = ImageChannel.One,
                                    };

                                    sysResourceModel.Value = JsonConvert.SerializeObject(CreateConfig);
                                    int ret1 = SysResourceDao.Instance.Save(sysResourceModel);
                                    if (ret1 < 0)
                                    {
                                        MessageBox.Show(Application.Current.GetActiveWindow(), "不允许创建没有Code的相机", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                    this.LoadPhyCamera();

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
                        CameraLicenseModel? cameraLicenseModel = licenses.Find(a => a.MacAddress == Path.GetFileNameWithoutExtension(openFileDialog.SafeFileName));
                        if (cameraLicenseModel == null)
                            cameraLicenseModel = new CameraLicenseModel();

                        cameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(openFileDialog.SafeFileName);
                        cameraLicenseModel.LicenseValue = File.ReadAllText(file);
                        cameraLicenseModel.CusTomerName = cameraLicenseModel.ColorVisionLincense.Licensee;
                        cameraLicenseModel.Model = cameraLicenseModel.ColorVisionLincense.DeviceMode;
                        cameraLicenseModel.ExpiryDate = cameraLicenseModel.ColorVisionLincense.ExpiryDateTime;

                        int ret = CameraLicenseDao.Instance.Save(cameraLicenseModel);

                        MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加许可证失败" : "添加许可证成功")}", "ColorVision");

                        var sysDictionaryModel = SysResourceDao.Instance.GetAll().Find(a => a.Code == cameraLicenseModel.MacAddress);
                        if (sysDictionaryModel == null)
                        {
                            sysDictionaryModel = new SysResourceModel();
                            sysDictionaryModel.Code = cameraLicenseModel.MacAddress;
                            sysDictionaryModel.Type = (int)ServiceTypes.PhyCamera;

                            var CreateConfig = new ConfigPhyCamera
                            {
                                CameraType = CameraType.LV_Q,
                                TakeImageMode = TakeImageMode.Measure_Normal,
                                ImageBpp = ImageBpp.bpp8,
                                Channel = ImageChannel.One,
                            };

                            sysDictionaryModel.Value = JsonConvert.SerializeObject(CreateConfig);

                            ret = SysResourceDao.Instance.Save(sysDictionaryModel);
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加物理相机失败" : "添加物理相机成功")}", "ColorVision");
                        }
                        this.LoadPhyCamera();
                    }
                    else
                    {
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), "不支持的许可文件后缀", "ColorVision");
                    }
                }
            }
            LoadPhyCamera();
        }


        public EventHandler Loaded { get; set; }

        public ObservableCollection<PhyCamera> PhyCameras { get; set; } = new ObservableCollection<PhyCamera>();
        public void LoadPhyCamera()
        {
            PhyCameras.Clear();
            var list = SysResourceDao.Instance.GetAllType((int)ServiceTypes.PhyCamera);
            foreach (var item in list)
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    PhyCameras.Add(new PhyCamera(item));
                }
            }

            foreach (var phycamrea in PhyCameras)
            {
                List<SysResourceModel> sysResourceModels = SysResourceDao.Instance.GetResourceItems(phycamrea.SysResourceModel.Id);
                foreach (var sysResourceModel in sysResourceModels)
                {
                    if (sysResourceModel.Type == (int)ServiceTypes.Group)
                    {
                        GroupResource groupResource = new(sysResourceModel);
                        phycamrea.AddChild(groupResource);
                        GroupResource.LoadgroupResource(groupResource);
                    }
                    else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 40)
                    {
                        CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                        phycamrea.AddChild(calibrationResource);
                    }
                    else
                    {
                        BaseFileResource calibrationResource = new(sysResourceModel);
                        phycamrea.AddChild(calibrationResource);
                    }
                }

            }
            Loaded?.Invoke(this, new EventArgs());
        }

        public static void LoadgroupResource(GroupResource groupResource)
        {
            List<SysResourceModel> sysResourceModels = SysResourceDao.Instance.GetGroupResourceItems(groupResource.SysResourceModel.Id);
            foreach (var sysResourceModel in sysResourceModels)
            {
                if (sysResourceModel.Type == (int)ServiceTypes.Group)
                {
                    GroupResource groupResource1 = new(sysResourceModel);
                    LoadgroupResource(groupResource1);
                    groupResource.AddChild(groupResource);
                }
                else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 40)
                {
                    CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
                else
                {
                    BaseResource calibrationResource = new(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
            }
            groupResource.SetCalibrationResource();
        }
    }
}
