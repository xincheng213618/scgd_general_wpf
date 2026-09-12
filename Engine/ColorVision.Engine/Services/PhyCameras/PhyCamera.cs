#pragma warning disable CA1863,CS0168,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Templates;
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
        private readonly CalibrationUploadRunner _calibrationUploadRunner = new();

        public ConfigPhyCamera Config { get; set; }

        [CommandDisplay("UploadCalibrationFiles", Order =100)]
        public RelayCommand UploadCalibrationCommand { get; set; }
        [CommandDisplay("CalibrationEditCommand", Order =102)]
        public RelayCommand CalibrationEditCommand { get; set; }
        [CommandDisplay("CaliTemplateSet",Order =101)]
        public RelayCommand CalibrationTemplateOpenCommand { get; set; }
        [CommandDisplay("CloneCalibrationTemplates", Order = 102)]
        public RelayCommand CloneCalibrationTemplatesCommand { get; set; }
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
        [CommandDisplay("PropertyEditorWindow", Order = -98)]
        public RelayCommand PropertyEditorEditCommand { get; set; }
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
            DeleteCommand = new RelayCommand(a => Delete());
            EditCommand = new RelayCommand(a =>
            {
                EditConfigPhyCamera window = new EditConfigPhyCamera(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (window.ShowDialog() == true)
                {
                    Save();
                }
            });

            PropertyEditorEditCommand = new RelayCommand(a => OpenPropertyEditor());

            CopyConfigCommand = new RelayCommand(a => Common.Clipboard.SetText(Config.ToJsonN()));
            ContentInit();

            UploadCalibrationCommand = new RelayCommand(a => UploadCalibration(a), a => !_calibrationUploadRunner.IsRunning);
            _calibrationUploadRunner.RunningStateChanged += (_, _) => RaiseUploadCalibrationCommandCanExecuteChanged();

            CalibrationParam.LoadResourceParams(CalibrationParams, SysResourceModel.Id);

            ResetCommand = new RelayCommand(a => Reset());

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
            CloneCalibrationTemplatesCommand = new RelayCommand(a =>
            {
                CalibrationTemplateCloneWindow window = new(this)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.ShowDialog();
            });

            UploadLicenseNetCommand = new RelayCommand(a => Task.Run(() => UploadLicenseNet()));
            OpenSettingDirectoryCommand = new RelayCommand(a => OpenSettingDirectory(),a=> Directory.Exists(Path.Combine(Config.FileServerCfg.FileBasePath, Code)));
            CreatResotreCommand = new RelayCommand(a => CreateRestore(), a => !_calibrationUploadRunner.IsRunning);
            LoadResotreCommand = new RelayCommand(a => LoadResotre(), a => !_calibrationUploadRunner.IsRunning);

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

        private void OpenPropertyEditor()
        {
            var editConfig = Config.Clone();
            editConfig.CFW = Config.CFW.CloneForEdit();
            editConfig.CFW.EnsureChannelCfgsForEdit();

            var propertyEditorWindow = new PropertyEditorWindow(editConfig)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            propertyEditorWindow.Submitted += (s, e) =>
            {
                editConfig.CFW.NormalizeChannelCfgsForSave();
                editConfig.CopyTo(Config);
                Save();
            };
            propertyEditorWindow.ShowDialog();
        }

        public async void CreateRestore()
        {
            if (_calibrationUploadRunner.IsRunning)
            {
                ShowCalibrationUploadBusy();
                return;
            }

            string destinationPath;
            using (System.Windows.Forms.SaveFileDialog saveFileDialog = new())
            {
                saveFileDialog.Filter = "ColorVision calibration restore (*.cvcal)|*.cvcal";
                saveFileDialog.DefaultExt = "cvcal";
                saveFileDialog.AddExtension = true;
                saveFileDialog.FileName = $"{Code}.cvcal";
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.Title = Properties.Resources.CreateRestorePoint;
                if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                destinationPath = saveFileDialog.FileName;
            }

            CalibrationExportPlan plan;
            try
            {
                plan = CalibrationArchivePlanBuilder.Create(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),
                    string.Format(Properties.Resources.CreateRestoreFileFailed, ex.Message),
                    Properties.Resources.CreateRestorePoint,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            bool started = await _calibrationUploadRunner.TryRunAsync(async () =>
            {
                ShowArchiveProgress(plan);
                try
                {
                    Progress<CalibrationExportProgress> progress = new(value =>
                    {
                        Msg = string.IsNullOrWhiteSpace(value.EntryPath)
                            ? $"正在创建还原点 {value.Percent}%…"
                            : $"正在创建还原点 {value.Percent}%：{Path.GetFileName(value.EntryPath)}";
                        if (value.Percent >= 100)
                        {
                            foreach (FileUploadInfo item in UploadList)
                                item.UploadStatus = UploadStatus.Completed;
                        }
                        else if (!string.IsNullOrWhiteSpace(value.EntryPath))
                        {
                            FileUploadInfo? current = UploadList.FirstOrDefault(item =>
                                string.Equals(item.FilePath, value.EntryPath, StringComparison.OrdinalIgnoreCase));
                            if (current != null)
                                current.UploadStatus = UploadStatus.Uploading;
                        }
                    });

                    await Task.Run(() => CalibrationExportArchive.CreateOrReplace(destinationPath, plan, progress));
                    NotifyUploadClosed();
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.RestorePointCreatedSuccessfully,
                        Properties.Resources.CreateRestorePoint);
                }
                catch (Exception ex)
                {
                    foreach (FileUploadInfo item in UploadList.Where(item => item.UploadStatus != UploadStatus.Completed))
                    {
                        item.UploadStatus = UploadStatus.Failed;
                    }
                    Msg = ex.Message;
                    log.Error(ex);
                    NotifyUploadClosed();
                    MessageBox.Show(Application.Current.GetActiveWindow(),
                        string.Format(Properties.Resources.CreateRestoreFileFailed, ex.Message),
                        Properties.Resources.CreateRestorePoint,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });

            if (!started)
                ShowCalibrationUploadBusy();
        }

        public async void LoadResotre()
        {
            if (_calibrationUploadRunner.IsRunning)
            {
                ShowCalibrationUploadBusy();
                return;
            }

            string archivePath;
            using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
            {
                openFileDialog.Filter = "ColorVision calibration restore (*.cvcal)|*.cvcal";
                openFileDialog.DefaultExt = "cvcal";
                openFileDialog.Multiselect = false;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.Title = Properties.Resources.LoadRestorePoint;
                if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                archivePath = openFileDialog.FileName;
            }

            string destinationPath = Path.Combine(Config.FileServerCfg.FileBasePath, Code, "cfg");
            await RunCalibrationPackageAsync(destinationPath, archivePath, restoreCameraSettings: true);
        }

        private void ShowArchiveProgress(CalibrationExportPlan plan)
        {
            UploadList.Clear();
            foreach (CalibrationExportFile file in plan.Files)
            {
                UploadList.Add(new FileUploadInfo
                {
                    FileName = file.EntryPath,
                    FilePath = file.EntryPath,
                    FileSize = MemorySize.MemorySizeText(MemorySize.FileSize(file.SourcePath))
                });
            }
            foreach (CalibrationExportText textEntry in plan.TextEntries)
            {
                UploadList.Add(new FileUploadInfo
                {
                    FileName = textEntry.EntryPath,
                    FilePath = textEntry.EntryPath,
                    FileSize = MemorySize.MemorySizeText(Encoding.UTF8.GetByteCount(textEntry.Content))
                });
            }

            Msg = "正在准备创建还原点…";
            UploadMsg uploadMsg = new(this)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Title = Properties.Resources.CreateRestorePoint
            };
            uploadMsg.Show();
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
            var ITemplate = new TemplateCalibrationParam(this);
            new TemplateEditorWindow(ITemplate) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        public void Reset()
        {
            if (MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.ClearDatabaseItems, Properties.Resources.Reset, MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
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
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.ConfirmDelete, Properties.Resources.Delete, MessageBoxButton.OKCancel) == MessageBoxResult.Cancel) return;
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

                if (CameraLicenseModel.DevCaliId is int calibrationId)
                {
                    DeviceCalibration? deviceCalibration = ServiceManager.Current?.DeviceServices
                        .OfType<DeviceCalibration>()
                        .FirstOrDefault(device => device.SysResourceModel.Id == calibrationId);
                    deviceCalibration?.RestartRCService();
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
                if (CameraLicenseModel.DevCameraId is int cameraId)
                {
                    DeviceCamera? deviceCamera = ServiceManager.Current?.DeviceServices
                        .OfType<DeviceCamera>()
                        .FirstOrDefault(device => device.SysResourceModel.Id == cameraId);
                    deviceCamera?.RestartRCService();
                }
            }
        }

        #region License

        public LicenseModel? CameraLicenseModel
        {
            get => _CameraLicenseModel;
            set
            {
                _CameraLicenseModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLicensed));
                OnPropertyChanged(nameof(LicenseState));
                OnPropertyChanged(nameof(LicenseSolidColorBrush));
                OnPropertyChanged(nameof(LicenseExpiryText));
                OnPropertyChanged(nameof(LicenseExpiryColor));
                OnPropertyChanged(nameof(LicenseStatusText));
                OnPropertyChanged(nameof(LicenseStatusBadgeText));
                OnPropertyChanged(nameof(LicenseStatusBrush));
                OnPropertyChanged(nameof(LicenseStatusBackgroundBrush));
                OnPropertyChanged(nameof(HasLicenseAlert));
                OnPropertyChanged(nameof(LicenseAlertText));
                OnPropertyChanged(nameof(ShowLicenseListTag));
                OnPropertyChanged(nameof(LicenseListTagText));
                OnPropertyChanged(nameof(LicenseListTagBrush));
                OnPropertyChanged(nameof(DeviceModeDisplayText));
                OnPropertyChanged(nameof(CameraListMetaText));
                OnPropertyChanged(nameof(LicenseSummaryMetaText));
            }
        }
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

        public string DeviceModeDisplayText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CameraLicenseModel?.ColorVisionLicense?.DeviceMode))
                    return CameraLicenseModel.ColorVisionLicense.DeviceMode;

                return Config.CameraModel.ToString();
            }
        }

        public string CameraInfoMetaText => string.Join(" · ", new[]
        {
            Config.CameraModel.ToString(),
            Config.CameraMode.ToString(),
            Config.TakeImageMode.ToString(),
            Config.Channel.ToString(),
            Config.ImageBpp.ToString()
        }.Where(item => !string.IsNullOrWhiteSpace(item)));

        public string CameraListMetaText
        {
            get
            {
                List<string> parts = new()
                {
                    Config.CameraModel.ToString(),
                    Config.CameraMode.ToString()
                };

                if (CameraLicenseModel?.ExpiryDate is DateTime expiryDate)
                    parts.Add($"{expiryDate:yyyy-MM-dd}");

                return string.Join(" · ", parts.Where(item => !string.IsNullOrWhiteSpace(item)));
            }
        }

        public string LicenseSummaryMetaText
        {
            get
            {
                string dateRange = GetLicenseDateRangeDisplayText();
                return string.IsNullOrWhiteSpace(dateRange) ? string.Empty : $"· {dateRange}";
            }
        }

        private string GetLicenseDateRangeDisplayText()
        {
            DateTime? createDate = CameraLicenseModel?.CreateDate;
            DateTime? expiryDate = CameraLicenseModel?.ExpiryDate;

            if (createDate is DateTime start && expiryDate is DateTime end)
                return $"{start:yyyy-MM-dd} - {end:yyyy-MM-dd}";

            if (createDate is DateTime onlyStart)
                return $"{onlyStart:yyyy-MM-dd}";

            if (expiryDate is DateTime onlyEnd)
                return $"{onlyEnd:yyyy-MM-dd}";

            return string.Empty;
        }

        public string LicenseStatusText
        {
            get
            {
                return LicenseState switch
                {
                    LicenseState.Unlicensed => Properties.Resources.LicenseStatusUnlicensed,
                    LicenseState.Expired => Properties.Resources.LicenseStatusExpired,
                    LicenseState.Invalid => Properties.Resources.LicenseStatusInvalid,
                    LicenseState.Licensed when CameraLicenseModel?.ExpiryDate is DateTime expiryDate && (expiryDate - DateTime.Now).Days <= 30 => Properties.Resources.LicenseStatusExpiringSoon,
                    LicenseState.Licensed => Properties.Resources.LicenseStatusValid,
                    _ => Properties.Resources.LicenseStatusInvalid,
                };
            }
        }

        public string LicenseStatusBadgeText
        {
            get
            {
                return LicenseState switch
                {
                    LicenseState.Unlicensed => Properties.Resources.LicenseStatusUnlicensed,
                    LicenseState.Expired when CameraLicenseModel?.ExpiryDate is DateTime expiryDate => string.Format(Properties.Resources.LicenseBadgeExpired, $"{expiryDate:yyyy-MM-dd}"),
                    LicenseState.Invalid => Properties.Resources.LicenseBadgeInvalid,
                    LicenseState.Licensed when CameraLicenseModel?.ExpiryDate is DateTime expiryDate && (expiryDate - DateTime.Now).Days <= 30 => string.Format(Properties.Resources.LicenseBadgeExpiringSoon, $"{expiryDate:yyyy-MM-dd}"),
                    LicenseState.Licensed => Properties.Resources.LicenseBadgeValid,
                    _ => Properties.Resources.LicenseBadgeInvalid,
                };
            }
        }

        public SolidColorBrush LicenseStatusBrush
        {
            get
            {
                return LicenseState switch
                {
                    LicenseState.Unlicensed => new SolidColorBrush(Colors.Red),
                    LicenseState.Expired => new SolidColorBrush(Colors.Red),
                    LicenseState.Invalid => new SolidColorBrush(Colors.Gray),
                    LicenseState.Licensed when CameraLicenseModel?.ExpiryDate is DateTime expiryDate && (expiryDate - DateTime.Now).Days <= 30 => new SolidColorBrush(Colors.Orange),
                    LicenseState.Licensed => new SolidColorBrush(Colors.Green),
                    _ => new SolidColorBrush(Colors.Gray),
                };
            }
        }

        public SolidColorBrush LicenseStatusBackgroundBrush
        {
            get
            {
                Color color = LicenseStatusBrush.Color;
                return new SolidColorBrush(Color.FromArgb(36, color.R, color.G, color.B));
            }
        }

        public bool HasLicenseAlert => !string.IsNullOrWhiteSpace(LicenseAlertText);

        public string LicenseAlertText
        {
            get
            {
                return LicenseState switch
                {
                    LicenseState.Unlicensed => Properties.Resources.LicenseAlertNoLicense,
                    LicenseState.Expired when CameraLicenseModel?.ExpiryDate is DateTime expiryDate => string.Format(Properties.Resources.LicenseAlertExpired, $"{expiryDate:yyyy-MM-dd}"),
                    LicenseState.Invalid => Properties.Resources.LicenseAlertInvalid,
                    LicenseState.Licensed when CameraLicenseModel?.ExpiryDate is DateTime expiryDate && (expiryDate - DateTime.Now).Days <= 30 => string.Format(Properties.Resources.LicenseAlertExpiringSoon, $"{expiryDate:yyyy-MM-dd}"),
                    _ => string.Empty,
                };
            }
        }

        public bool ShowLicenseListTag => !string.IsNullOrWhiteSpace(LicenseListTagText);

        public string LicenseListTagText
        {
            get
            {
                return LicenseState switch
                {
                    LicenseState.Unlicensed => Properties.Resources.LicenseStatusUnlicensed,
                    LicenseState.Expired => Properties.Resources.LicenseListTagExpired,
                    LicenseState.Invalid => Properties.Resources.LicenseListTagInvalid,
                    LicenseState.Licensed when CameraLicenseModel?.ExpiryDate is DateTime expiryDate && (expiryDate - DateTime.Now).Days <= 30 => Properties.Resources.LicenseStatusExpiringSoon,
                    _ => string.Empty,
                };
            }
        }

        public SolidColorBrush LicenseListTagBrush => LicenseStatusBrush;

        public string LicenseExpiryText
        {
            get
            {
                if (CameraLicenseModel == null || CameraLicenseModel.ExpiryDate == null)
                    return Properties.Resources.LicenseStatusUnlicensed;

                var expiryDate = CameraLicenseModel.ExpiryDate.Value;
                if (expiryDate < DateTime.Now)
                    return string.Format(Properties.Resources.LicenseExpiryExpired, $"{expiryDate:yyyy-MM-dd}");

                var daysRemaining = (expiryDate - DateTime.Now).Days;
                if (daysRemaining <= 30)
                    return string.Format(Properties.Resources.LicenseExpiryDaysRemaining, daysRemaining, $"{expiryDate:yyyy-MM-dd}");

                return string.Format(Properties.Resources.LicenseExpiryValidUntil, $"{expiryDate:yyyy-MM-dd}");
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
            openFileDialog.Title = string.Format(Properties.Resources.SelectLicenseFileWithCode, SysResourceModel.Code);
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
                            string? previousLicenseValue = CameraLicenseModel?.LicenseValue;
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
                            }

                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? Properties.Resources.AddFailed : Properties.Resources.AddSuccess)}", Properties.Resources.ModifyLicense);
                            if (ShouldRestartServicesAfterLicenseUpdate(previousLicenseValue, CameraLicenseModel.LicenseValue, ret)
                                && MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.Camera_ConfirmRestartService, Properties.Resources.ModifyLicense, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                MqttRCService.GetInstance().RestartServices();
                            }
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
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{Properties.Resources.ExtractionFailed} :{ex.Message}", Properties.Resources.ModifyLicense);
                    return false;
                }
            }
            else if (Path.GetExtension(filepath) == ".lic")
            {
                string Code = Path.GetFileNameWithoutExtension(filepath);
                if (Code == SysResourceModel.Code)
                {
                    CameraLicenseModel = PhyLicenseDao.Instance.GetByMAC(SysResourceModel.Code);
                    string? previousLicenseValue = CameraLicenseModel?.LicenseValue;
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
                    }
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? Properties.Resources.AddFailed : Properties.Resources.UpdateSuccess)}", Properties.Resources.ModifyLicense);
                    if (ShouldRestartServicesAfterLicenseUpdate(previousLicenseValue, CameraLicenseModel.LicenseValue, ret)
                        && MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.Camera_ConfirmRestartService, Properties.Resources.ModifyLicense, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        MqttRCService.GetInstance().RestartServices();
                    }
                    if (ret == -1)
                    {
                        return false;
                    }
                    return true;
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.LicenseNotSupportedForCamera, Properties.Resources.ModifyLicense);
                }
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.UnsupportedLicenseFileExtension, Properties.Resources.ModifyLicense);
            }
            return false;
        }

        internal static bool ShouldRestartServicesAfterLicenseUpdate(string? previousLicenseValue, string? currentLicenseValue, int saveResult)
        {
            if (saveResult != 1)
                return false;

            return !string.Equals(previousLicenseValue?.Trim(), currentLicenseValue?.Trim(), StringComparison.Ordinal);
        }

        public void CopyLicense()
        {
            if (CameraLicenseModel == null || string.IsNullOrEmpty(CameraLicenseModel.LicenseValue))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.NoLicenseAvailable, Properties.Resources.CopyLicense);
                return;
            }

            try
            {
                Common.Clipboard.SetText(CameraLicenseModel.LicenseValue);
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.LicenseCopiedToClipboard, Properties.Resources.CopyLicense);
            }
            catch (Exception ex)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{Properties.Resources.CopyLicenseFailed}: {ex.Message}", Properties.Resources.CopyLicense);
            }
        }

        public void ExportLicense()
        {
            if (CameraLicenseModel == null || string.IsNullOrEmpty(CameraLicenseModel.LicenseValue))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.NoLicenseAvailable, Properties.Resources.ExportLicense);
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
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.LicenseExportedSuccessfully, Properties.Resources.ExportLicense);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{Properties.Resources.ExportLicenseFailed}: {ex.Message}", Properties.Resources.ExportLicense);
            }
        }


        #endregion



        public void UploadCalibration(object sender)
        {
            if (_calibrationUploadRunner.IsRunning)
            {
                ShowCalibrationUploadBusy();
                return;
            }

            string DesPath = Path.Combine(Config.FileServerCfg.FileBasePath, Code, "cfg");
            UploadWindow uploadwindow = new UploadWindow(Properties.Resources.CalibrationFileFilter) { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += async (s, e) =>
            {
                string uploadfilepath = e.UploadFilePath;
                await UploadDataAsync(DesPath, uploadfilepath);
            };
            uploadwindow.ShowDialog();
        }

        public string Msg { get => _Msg; set { _Msg = value; Application.Current.Dispatcher.Invoke(() => OnPropertyChanged()); } }
        private string _Msg;

        public event EventHandler UploadClosed;
        public ObservableCollection<FileUploadInfo> UploadList { get; set; } = new ObservableCollection<FileUploadInfo>();

        internal void NotifyUploadClosed()
        {
            void RaiseUploadClosed()
            {
                try
                {
                    UploadClosed?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    log.Error("Failed to notify calibration upload completion.", ex);
                }
            }

            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.CheckAccess())
                {
                    RaiseUploadClosed();
                }
                else if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                {
                    dispatcher.Invoke(RaiseUploadClosed);
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to dispatch calibration upload completion.", ex);
            }
        }

        public void UploadData(string DesPath, string UploadFilePath) => _ = UploadDataAsync(DesPath, UploadFilePath);

        public Task UploadDataAsync(string DesPath, string UploadFilePath) =>
            RunCalibrationPackageAsync(DesPath, UploadFilePath, restoreCameraSettings: false);

        private async Task RunCalibrationPackageAsync(string DesPath, string UploadFilePath, bool restoreCameraSettings)
        {
            bool started = await _calibrationUploadRunner.TryRunAsync(async () =>
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UploadList.Clear();
                        UploadMsg uploadMsg = new UploadMsg(this);
                        if (restoreCameraSettings)
                        {
                            uploadMsg.Owner = Application.Current.GetActiveWindow();
                            uploadMsg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        }
                        else
                        {
                            uploadMsg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        }
                        uploadMsg.Title = restoreCameraSettings
                            ? Properties.Resources.LoadRestorePoint
                            : Properties.Resources.CalibrationFileManagement;
                        uploadMsg.Show();
                    });
                    await Task.Run(() => UploadDataCoreAsync(DesPath, UploadFilePath, restoreCameraSettings));
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    Msg = ex.Message;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, Properties.Resources.CalibrationFileManagement);
                    });
                    NotifyUploadClosed();
                }
            });

            if (!started)
            {
                ShowCalibrationUploadBusy();
            }
        }

        private static void ShowCalibrationUploadBusy()
        {
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.AlreadySentPleaseWait, Properties.Resources.CalibrationFileManagement));
        }

        internal void RaiseUploadCalibrationCommandCanExecuteChanged()
        {
            RaiseCanExecuteChangedOnUiThread(() =>
            {
                try
                {
                    UploadCalibrationCommand.RaiseCanExecuteChanged();
                    CreatResotreCommand.RaiseCanExecuteChanged();
                    LoadResotreCommand.RaiseCanExecuteChanged();
                }
                catch (Exception ex)
                {
                    log.Error("Failed to update calibration upload command state.", ex);
                }
            });
        }

        internal static void RaiseCanExecuteChangedOnUiThread(Action raiseCanExecuteChanged)
        {
            ArgumentNullException.ThrowIfNull(raiseCanExecuteChanged);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                raiseCanExecuteChanged();
            }
            else if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                dispatcher.Invoke(raiseCanExecuteChanged);
            }
        }

        private async Task UploadDataCoreAsync(string DesPath, string UploadFilePath, bool restoreCameraSettings)
        {
            Msg = Properties.Resources.ExtractingFilePleaseWait;
            await Task.Delay(10);
            if (!File.Exists(UploadFilePath))
                throw new FileNotFoundException(Properties.Resources.ExtractionFailedMessage, UploadFilePath);

            using CalibrationUploadWorkspace workspace = CalibrationUploadWorkspace.Create();
            string path = workspace.DirectoryPath;
            CalibrationExportArchive.ExtractToDirectory(
                UploadFilePath,
                path,
                new DelegateProgress<CalibrationExportProgress>(value =>
                {
                    Msg = string.IsNullOrWhiteSpace(value.EntryPath)
                        ? $"正在解压 {value.Percent}%…"
                        : $"正在解压 {value.Percent}%：{Path.GetFileName(value.EntryPath)}";
                }));
            Msg = Properties.Resources.ParsingCalibrationFilePleaseWait;

            if (restoreCameraSettings)
            {
                DesPath = ApplyRestorePackageSettings(path);
            }
            Directory.CreateDirectory(DesPath);

            try
            {
                    string Calibrationcfg = path + "\\Calibration.cfg";

                    Dictionary<string, List<ZipCalibrationItem>> AllCalFiles = JsonConvert.DeserializeObject<Dictionary<string, List<ZipCalibrationItem>>>(ReadCalibrationPackageText(Calibrationcfg));

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
                            case CalibrationType.AngleShift:
                                FilePath = path + "\\Calibration\\" + "AngleShift\\" + item.FileName;
                                break;
                            case CalibrationType.Empty_Num:
                                break;
                            default:
                                break;
                        }

                        FileUploadInfo uploadMeta = UploadList.First(a => a.FileName == item.Title);
                        uploadMeta.FilePath = FilePath;
                        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
                        {
                            string missingSourceMessage = string.Format(Properties.Resources.Calibration_PackageFileNotFound, item.Title, FilePath);
                            uploadMeta.FileSize = "0 B";
                            uploadMeta.UploadStatus = UploadStatus.Failed;
                            Msg = missingSourceMessage;
                            log.Warn(missingSourceMessage);
                            continue;
                        }

                        uploadMeta.FileSize = MemorySize.MemorySizeText(MemorySize.FileSize(FilePath));
                        uploadMeta.UploadStatus = UploadStatus.Uploading;
                        Msg = string.Format(Properties.Resources.UploadingCalibrationFilePleaseWait, item.Title);
                        await Task.Delay(10);

                        try
                        {
                            string FileName = Path.GetFileName(FilePath);
                            string DesFilePath = Path.Combine(DesPath, FileName);
                            File.Copy(FilePath, DesFilePath, true);
                            File.Delete(FilePath);
                            int resourceType = (int)item.CalibrationType.ToResouceType();
                            string remark = item.ToJsonN(new JsonSerializerSettings());

                            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                            SysResourceModel sysResourceModel = db.Queryable<SysResourceModel>()
                                .Where(a => a.Pid == SysResourceModel.Id
                                            && a.Type == resourceType
                                            && a.Name == item.Title
                                            && a.IsDelete == false
                                            && a.IsEnable == true)
                                .First();

                            if (sysResourceModel == null)
                            {
                                sysResourceModel = new();
                                sysResourceModel.Name = item.Title;
                                string resourceCode;
                                do
                                {
                                    resourceCode = $"{Id}_{resourceType}_{Guid.NewGuid():N}";
                                }
                                while (db.Queryable<SysResourceModel>().Any(a => a.Code == resourceCode));

                                sysResourceModel.Code = resourceCode;
                                sysResourceModel.Type = resourceType;
                                sysResourceModel.Pid = SysResourceModel.Id;
                                sysResourceModel.TenantId = SysResourceModel.TenantId;
                                sysResourceModel.Value = FileName;
                                sysResourceModel.CreateDate = DateTime.Now;
                                sysResourceModel.Remark = remark;
                                int ret = SysResourceDao.Instance.Save(sysResourceModel);
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(sysResourceModel.Code))
                                {
                                    string resourceCode;
                                    do
                                    {
                                        resourceCode = $"{Id}_{resourceType}_{Guid.NewGuid():N}";
                                    }
                                    while (db.Queryable<SysResourceModel>().Any(a => a.Code == resourceCode));

                                    sysResourceModel.Code = resourceCode;
                                }

                                sysResourceModel.Value = FileName;
                                sysResourceModel.Remark = remark;
                                db.Updateable(sysResourceModel).ExecuteCommand();
                            }

                            CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                            calibrationResource.SysResourceModel.Code = sysResourceModel.Code;
                            calibrationResource.SysResourceModel.Value = sysResourceModel.Value;
                            calibrationResource.SysResourceModel.Remark = sysResourceModel.Remark;
                            calibrationResource.Config = JsonConvert.DeserializeObject<CalibrationFileConfig>(remark) ?? new CalibrationFileConfig();

                            if (!VisualChildren.OfType<CalibrationResource>().Any(resource => resource.SysResourceModel.Id == calibrationResource.SysResourceModel.Id))
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    AddChild(calibrationResource);
                                });
                            }

                            keyValuePairs2.TryAdd(item.Title, calibrationResource);
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
                    foreach (var item2 in directoryInfo.Exists ? directoryInfo.GetFiles() : [])
                    {
                        try
                        {
                            ZipCalibrationGroup zipCalibrationGroup;
                            try
                            {
                                zipCalibrationGroup = new ZipCalibrationGroup();
                                zipCalibrationGroup.List = JsonConvert.DeserializeObject<List<ZipCalibrationItem>>(ReadCalibrationPackageText(item2.FullName)) ?? new List<ZipCalibrationItem>();
                            }
                            catch (Exception ex)
                            {
                                zipCalibrationGroup = JsonConvert.DeserializeObject<ZipCalibrationGroup>(ReadCalibrationPackageText(item2.FullName));
                            }

                            if (zipCalibrationGroup != null)
                            {
                                string filePath = Path.GetFileNameWithoutExtension(item2.FullName);

                                GroupResource? existingGroup = VisualChildren
                                    .OfType<GroupResource>()
                                    .FirstOrDefault(group => group.Name == filePath);
                                if (existingGroup != null && !restoreCameraSettings)
                                {
                                    log.Info($"{filePath} Exit");
                                    continue;
                                }
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    GroupResource? groupResource = existingGroup ?? GroupResource.AddGroupResource(this, filePath);
                                    if (groupResource != null)
                                    {
                                        if (existingGroup != null)
                                            groupResource.VisualChildren.Clear();

                                        foreach (var item1 in zipCalibrationGroup.List)
                                        {
                                            if (keyValuePairs2.TryGetValue(item1.Title, out var colorVisionVCalibratioItems))
                                            {
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
                                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, Properties.Resources.CalibrationFileManagement);
                            });
                        }
                    }
                    Msg = Properties.Resources.UploadFinished;
                    bool completed = !UploadList.Any(a => a.UploadStatus == UploadStatus.Failed);
                    if (completed)
                    {
                        await Task.Delay(500);
                        NotifyUploadClosed();
                        if (restoreCameraSettings)
                        {
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                                Application.Current.GetActiveWindow(),
                                Properties.Resources.RestoreSucceeded,
                                Properties.Resources.LoadRestorePoint));
                        }
                    }
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                    Msg = ex.Message;

                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, Properties.Resources.CalibrationFileManagement);
                    } );
                    NotifyUploadClosed();
                    return;
                }
        }

        private string ApplyRestorePackageSettings(string packageDirectory)
        {
            string cameraConfigPath = Path.Combine(packageDirectory, "Camera.cfg");
            if (!File.Exists(cameraConfigPath))
            {
                cameraConfigPath = Path.Combine(packageDirectory, "CameraConfig.cfg");
            }
            if (!File.Exists(cameraConfigPath))
                throw new InvalidDataException("还原点缺少 Camera.cfg。");

            ConfigPhyCamera restoredConfig = JsonConvert.DeserializeObject<ConfigPhyCamera>(ReadCalibrationPackageText(cameraConfigPath))
                ?? throw new InvalidDataException("还原点中的 Camera.cfg 无效。");
            string restoredDestinationPath = Path.Combine(restoredConfig.FileServerCfg.FileBasePath, Code, "cfg");
            Directory.CreateDirectory(restoredDestinationPath);

            string licensePath = Path.Combine(packageDirectory, $"{Code}.lic");
            string? licenseValue = File.Exists(licensePath)
                ? File.ReadAllText(licensePath, Encoding.UTF8)
                : null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                restoredConfig.CopyTo(Config);
                SaveConfig();

                if (licenseValue != null)
                {
                    CameraLicenseModel = PhyLicenseDao.Instance.GetByMAC(Code) ?? new LicenseModel();
                    CameraLicenseModel.LiceType = 0;
                    CameraLicenseModel.MacAddress = Code;
                    CameraLicenseModel.LicenseValue = licenseValue;
                    CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLicense.Licensee;
                    CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLicense.DeviceMode;
                    CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLicense.ExpiryDateTime;
                    if (PhyLicenseDao.Instance.Save(CameraLicenseModel) < 0)
                        throw new InvalidOperationException(Properties.Resources.AddFailed);
                    RefreshLicense();
                }
            });

            return restoredDestinationPath;
        }

        private static string ReadCalibrationPackageText(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            try
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding("gbk").GetString(bytes);
            }
        }

        private sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
        {
            public void Report(T value) => report(value);
        }

        public UserControl GetDeviceInfo()
        {
            return new InfoPhyCamera(this);
        }

        public void ContentInit()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Edit, Command = EditCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.PropertyEditorFallback, Command = PropertyEditorEditCommand });
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
            else
            {
                Config.CFW.NormalizeChannelCfgsForSave();
            }

            if (Config.CFW.IsBingNDDevice)
            {
                Config.CFW.SzComName = string.Empty;
            }
            else
            {
                Config.CFW.NDBindDeviceCode = string.Empty;
            }

            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            SysResourceDao.Instance.Save(SysResourceModel);

            OnPropertyChanged(nameof(DeviceModeDisplayText));
            OnPropertyChanged(nameof(CameraInfoMetaText));
            OnPropertyChanged(nameof(CameraListMetaText));

            ConfigChanged?.Invoke(this, Config);
        }
        public override void Save()
        {
            base.Save();
            SaveConfig();
        }

    }
}
