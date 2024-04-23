using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Msg;
using ColorVision.Services.PhyCameras.Templates;
using ColorVision.Services.PhyCameras.Configs;
using ColorVision.Services.RC;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Services.Devices.Calibration;
using log4net;
using ColorVision.Services.Devices;
using System.IO.Compression;
using ColorVision.Themes.Controls;
using ColorVision.Services.Templates;
using cvColorVision;
using ColorVision.Services.Type;
using ColorVision.Common.MVVM.Json;
using ColorVision.Services.Devices.PG;
using ColorVision.Services.PhyCameras.Dao;
using System.Linq;

namespace ColorVision.Services.PhyCameras
{
    public class PhyCamera : BaseResource,ITreeViewItem, IUploadMsg, ICalibrationService<BaseResourceObject>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceCalibration));

        public ConfigPhyCamera Config { get; set; }

        public RelayCommand UploadCalibrationCommand { get; set; }
        public RelayCommand CalibrationEditCommand { get; set; }
        public RelayCommand ResourceManagerCommand { get; set; }

        public RelayCommand UploadLincenseCommand { get; set; }
        public RelayCommand RefreshLincenseCommand { get; set; }

        public RelayCommand EditCommand { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; } = new ObservableCollection<TemplateModel<CalibrationParam>>();

        public PhyCamera(SysResourceModel sysResourceModel):base(sysResourceModel)
        {
            Config = BaseResourceObjectExtensions.TryDeserializeConfig<ConfigPhyCamera>(SysResourceModel.Value);
            DeleteCommand = new RelayCommand(a => Delete());
            EditCommand = new RelayCommand(a =>
            {
                EditPhyCamera window = new EditPhyCamera(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });
            ContentInit();

            ResourceManagerCommand = new RelayCommand(a =>
            {
                ResourceManager resourceManager = new ResourceManager(this) { Owner = WindowHelpers.GetActiveWindow() };
                resourceManager.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                resourceManager.ShowDialog();
            });

            UploadCalibrationCommand = new RelayCommand(a => UploadCalibration(a));

            TemplateControl.GetInstance().LoadModCabParam(CalibrationParams, SysResourceModel.Id, ModMasterType.Calibration);

            CalibrationEditCommand = new RelayCommand(a =>
            {
                CalibrationEdit CalibrationEdit = new CalibrationEdit(this);
                CalibrationEdit.Show();
            });

            UploadLincenseCommand = new RelayCommand(a => UploadLincense());
            RefreshLincenseCommand = new RelayCommand(a => RefreshLincense());

        }

        #region License
        public ObservableCollection<CameraLicenseModel> LicenseModels { get; set; } = new ObservableCollection<CameraLicenseModel>();

        public void RefreshLincense()
        {
            LicenseModels.Clear();
            foreach (var item in CameraLicenseDao.Instance.GetAllByPid(SysResourceModel.Id))
            {
                LicenseModels.Add(item);
            };
        }

        private void UploadLincense()
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
                                CameraLicenseModel cameraLicenseModel = new CameraLicenseModel();
                                cameraLicenseModel.DevCameraId = SysResourceModel.Id;
                                cameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(item.FullName);

                                using var stream = item.Open();
                                using var reader = new StreamReader(stream, Encoding.UTF8); // 假设文件编码为UTF-8
                                cameraLicenseModel.LicenseValue = reader.ReadToEnd();

                                cameraLicenseModel.CusTomerName = cameraLicenseModel.ColorVisionLincense.Licensee;
                                cameraLicenseModel.Model = cameraLicenseModel.ColorVisionLincense.DeviceMode;
                                cameraLicenseModel.ExpiryDate = cameraLicenseModel.ColorVisionLincense.ExpiryDateTime;
                                if (CameraLicenseDao.Instance.GetAllByMAC(cameraLicenseModel.MacAddress, SysResourceModel.Id).Count == 0)
                                {
                                    int ret = CameraLicenseDao.Instance.Save(cameraLicenseModel);

                                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "添加成功")}", "ColorVision");
                                }
                                else
                                {
                                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} 重复添加", "ColorVision");
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
                        CameraLicenseModel cameraLicenseModel = new CameraLicenseModel();
                        cameraLicenseModel.DevCameraId = SysResourceModel.Id;
                        cameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(openFileDialog.SafeFileName);
                        cameraLicenseModel.LicenseValue = File.ReadAllText(file);
                        cameraLicenseModel.CusTomerName = cameraLicenseModel.ColorVisionLincense.Licensee;
                        cameraLicenseModel.Model = cameraLicenseModel.ColorVisionLincense.DeviceMode;
                        cameraLicenseModel.ExpiryDate = cameraLicenseModel.ColorVisionLincense.ExpiryDateTime;

                        if (CameraLicenseDao.Instance.GetAllByMAC(cameraLicenseModel.MacAddress, SysResourceModel.Id).Count == 0)
                        {
                            int ret = CameraLicenseDao.Instance.Save(cameraLicenseModel);
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "添加成功")}", "ColorVision");
                        }
                        else
                        {
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{cameraLicenseModel.MacAddress} 重复添加", "ColorVision");
                        }
                        RefreshLincense();

                    }
                    else
                    {
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), "不支持的许可文件后缀", "ColorVision");
                    }

                }
            }
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
            UploadWindow uploadwindow = new UploadWindow("校正文件(*.zip, *.cvcal)|*.zip;*.cvcal") { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                if (s is Upload upload)
                {
                    UploadMsg uploadMsg = new UploadMsg(this);
                    uploadMsg.Show();
                    string path = upload.UploadFilePath;
                    Task.Run(() => UploadData(path));
                }
            };
            uploadwindow.ShowDialog();
        }

        public string Msg { get => _Msg; set { _Msg = value; Application.Current.Dispatcher.Invoke(() => NotifyPropertyChanged()); } }
        private string _Msg;

        public event EventHandler UploadClosed;
        public ObservableCollection<string> UploadList { get; set; } = new ObservableCollection<string>();
        public async void UploadData(string UploadFilePath)
        {
            Msg = "正在解压文件：" + " 请稍后...";
            await Task.Delay(10);
            if (File.Exists(UploadFilePath))
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\ColorVision\\Cacahe";
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

                string Cameracfg = path + "\\Camera.cfg";

                string Calibrationcfg = path + "\\Calibration.cfg";

                Dictionary<string, List<ZipCalibrationItem>> AllCalFiles = JsonConvert.DeserializeObject<Dictionary<string, List<ZipCalibrationItem>>>(File.ReadAllText(Calibrationcfg, Encoding.GetEncoding("gbk")));

                Dictionary<string, CalibrationResource> keyValuePairs2 = new Dictionary<string, CalibrationResource>();

                if (AllCalFiles != null)
                {
                    foreach (var item in AllCalFiles)
                    {
                        foreach (var item1 in item.Value)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                UploadList.Add(item1.Title);
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


                            Msg = "正在上传校正文件：" + item1.Title + " 请稍后...";
                            await Task.Delay(10);

                            switch (item1.CalibrationType)
                            {
                                case CalibrationType.DarkNoise:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.DarkNoise);
                                    break;
                                case CalibrationType.DefectWPoint:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.DefectPoint);
                                    break;
                                case CalibrationType.DefectBPoint:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.DefectPoint);
                                    break;
                                case CalibrationType.DefectPoint:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.DefectPoint);
                                    break;
                                case CalibrationType.DSNU:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.DSNU);
                                    break;
                                case CalibrationType.Uniformity:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.Uniformity);
                                    break;
                                case CalibrationType.Luminance:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.Luminance);
                                    break;
                                case CalibrationType.LumOneColor:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.LumOneColor);
                                    break;
                                case CalibrationType.LumFourColor:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.LumFourColor);
                                    break;
                                case CalibrationType.LumMultiColor:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.LumMultiColor);
                                    break;
                                case CalibrationType.LumColor:
                                    break;
                                case CalibrationType.Distortion:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.Distortion);
                                    break;
                                case CalibrationType.ColorShift:
                                    msgRecord = await MQTTFileUpload.GetInstance().UploadCalibrationFileAsync(item1.Title, FilePath, (int)ServiceTypes.ColorShift);
                                    break;
                                case CalibrationType.Empty_Num:
                                    break;
                                default:
                                    break;
                            }

                            if (msgRecord != null && msgRecord.MsgRecordState == MsgRecordState.Success)
                            {
                                string FileName = msgRecord.MsgReturn.Data.FileName;

                                SysResourceModel sysResourceModel = new SysResourceModel();
                                sysResourceModel.Name = item1.Title;
                                sysResourceModel.Code = md5;
                                sysResourceModel.Type = (int)item1.CalibrationType.ToResouceType();
                                sysResourceModel.Pid = SysResourceModel.Id;
                                sysResourceModel.Value = Path.GetFileName(FileName);
                                sysResourceModel.CreateDate = DateTime.Now;
                                sysResourceModel.Remark = item1.ToJson();
                                SysResourceDao.Instance.Save(sysResourceModel);
                                if (sysResourceModel != null)
                                {
                                    CalibrationResource calibrationResource = new CalibrationResource(sysResourceModel);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        AddChild(calibrationResource);
                                    });
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
                        ZipCalibrationGroup zipCalibrationGroup;
                        try
                        {
                            zipCalibrationGroup = new ZipCalibrationGroup();
                            zipCalibrationGroup.ZipCalibrationItems = JsonConvert.DeserializeObject<List<ZipCalibrationItem>>(File.ReadAllText(item2.FullName, Encoding.GetEncoding("gbk")));
                        }
                        catch (Exception ex)
                        {
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
                            GroupResource groupResource = GroupResource.AddGroupResource(this, filePath);
                            if (groupResource != null)
                            {
                                foreach (var item1 in zipCalibrationGroup.ZipCalibrationItems)
                                {
                                    if (keyValuePairs2.TryGetValue(item1.Title, out var colorVisionVCalibratioItems))
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            groupResource.AddChild(colorVisionVCalibratioItems);
                                        });
                                    }
                                }
                                groupResource.SetCalibrationResource(this);
                            }
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
                await Task.Delay(100);
                Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
            }
        }








        public UserControl GetDeviceInfo() => new InfoPhyCamera(this);

        public override void Delete()
        {
            base.Delete();
            PhyCameraManager.GetInstance().PhyCameras.Remove(this);
        }

        public void ContentInit()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Edit, Command = EditCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Delete, Command = DeleteCommand });

        }

        public void SaveConfig()
        {
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            SysResourceDao.Instance.Save(SysResourceModel);
        }
        public override void Save()
        {
            base.Save();
            SaveConfig();
        }

    }
}
