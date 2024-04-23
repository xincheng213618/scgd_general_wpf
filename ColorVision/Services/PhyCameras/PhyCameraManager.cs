using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.PhyCameras.Dao;
using ColorVision.Services.Type;
using System;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;

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
            CreateWindow createWindow = new CreateWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
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
                            ret = SysResourceDao.Instance.Save(sysDictionaryModel);
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加物理相机失败" : "添加物理相机成功")}", "ColorVision");
                        }
                    }
                    else
                    {
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), "不支持的许可文件后缀", "ColorVision");
                    }
                }
            }
            LoadPhyCamera();
        }

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
        }
    }
}
