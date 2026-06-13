#pragma warning disable CA1805,CA1806,CA1822,CA1863,CS0219
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using cvColorVision;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras.Licenses
{

    public class LicenseManagerViewModel : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LicenseManagerViewModel));

        public ObservableCollection<LicenseViewModel> Licenses { get; set; } = new ObservableCollection<LicenseViewModel>();

        public LicenseViewModel? SelectedLicense { get => _SelectedLicense; set { _SelectedLicense = value; OnPropertyChanged(); } }
        private LicenseViewModel? _SelectedLicense;

        public RelayCommand RefreshCommand { get; set; }
        public RelayCommand ImportCommand { get; set; }
        public RelayCommand ExportSelectedCommand { get; set; }
        public RelayCommand DeleteSelectedCommand { get; set; }
        public RelayCommand CopyLicenseCommand { get; set; }
        public RelayCommand GetCameraLicenseCommand { get; set; }
        public RelayCommand GetSpectrumLicenseCommand { get; set; }

        public RelayCommand SaveToLincenseCommand { get; set; }

        public string LicenseCountText => string.Format(Properties.Resources.LicenseCountFormat, Licenses.Count);

        public LicenseManagerViewModel()
        {
            Licenses.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LicenseCountText));
            RefreshCommand = new RelayCommand(a => LoadLicenses());
            ImportCommand = new RelayCommand(a => ImportLicense());
            ExportSelectedCommand = new RelayCommand(a => ExportSelected(), a => SelectedLicense != null);
            DeleteSelectedCommand = new RelayCommand(a => DeleteSelected(), a => SelectedLicense != null);
            CopyLicenseCommand = new RelayCommand(a => CopyLicense(), a => SelectedLicense != null);
            GetCameraLicenseCommand = new RelayCommand(a=> GetCameraLicense());
            GetSpectrumLicenseCommand = new RelayCommand(a => GetSpectrumLicense());

            SaveToLincenseCommand = new RelayCommand(a=> SaveToLincense());

            LoadLicenses();
        }


        public void SaveToLincense()
        {
            // 1. 让用户选择保存目录，默认定位到程序目录

            Microsoft.Win32.OpenFolderDialog dialog = new();

            dialog.Multiselect = false;
            dialog.Title = Properties.Resources.SelectSaveFolder;
            dialog.DefaultDirectory = AppDomain.CurrentDomain.BaseDirectory;
            dialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            // Show open folder dialog box
            bool? result = dialog.ShowDialog();

            // 如果用户取消，直接返回
            if (result !=true) return;

            // 取用户选中的目录
            string licenseDir = Path.Combine(dialog.FolderName, "lincense");

            try
            {
                // 检查是否有写入权限（简单判断：如果是管理员或者目录不在受保护区域，通常可以直接写）
                // 但最稳妥的是直接尝试写，如果报错UnauthorizedAccessException再提权，或者预先判断IsAdministrator
                if (ColorVision.Common.Utilities.Tool.IsAdministrator())
                {
                    WriteLicensesToDir(licenseDir);
                }
                else
                {
                    // 尝试创建一个测试文件来验证是否有权限
                    bool hasPermission = false;
                    try
                    {
                        if (!Directory.Exists(licenseDir))
                        {
                            // 尝试创建目录
                            Directory.CreateDirectory(licenseDir);
                        }

                        string testFile = Path.Combine(licenseDir, Guid.NewGuid().ToString() + ".tmp");
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                        hasPermission = true;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        hasPermission = false;
                    }
                    catch (Exception)
                    {
                        // 其他错误暂且认为没权限或路径非法
                        hasPermission = false;
                    }

                    if (hasPermission)
                    {
                        WriteLicensesToDir(licenseDir);
                    }
                    else
                    {
                        // 没有权限，使用命令行提权方案
                        // 1. 先写到 Temp 目录
                        string tempDir = Path.Combine(Path.GetTempPath(), "ColorVisionLicenses_" + Guid.NewGuid().ToString());
                        WriteLicensesToDir(tempDir);

                        // 2. 调用 CMD (runas) 将 Temp 目录内容 xcopy 到目标目录
                        // 注意：目标路径可能包含空格，需要引号
                        string src = tempDir;
                        string dst = licenseDir;

                        // 构建批处理命令：创建目录 -> 复制文件 -> 删除临时目录
                        // /E 复制目录和子目录 /Y 覆盖不提示 /I 如果目标不存在且复制多个文件则假定目标是目录
                        string cmdArgs = $"/c xcopy \"{src}\" \"{dst}\" /E /Y /I && rmdir /s /q \"{src}\"";

                        var psi = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = cmdArgs,
                            UseShellExecute = true,
                            Verb = "runas", // 提权
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        try
                        {
                            var proc = Process.Start(psi);
                            proc?.WaitForExit();
                            MessageBox.Show(Properties.Resources.LicenseExportedByAdmin, Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Information);
                            return; // 提权执行后直接返回，不由下方统一提示
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            MessageBox.Show(Properties.Resources.UserCancelledOrElevationFailed, Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                MessageBox.Show(Properties.Resources.LicenseExportCompleted, Properties.Resources.Hint, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.ExportFailedMessage, ex.Message), Properties.Resources.Failure, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WriteLicensesToDir(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            foreach (var vm in Licenses)
            {
                if (string.IsNullOrWhiteSpace(vm.MacAddress))
                    continue;

                string fileName = $"{vm.MacAddress}.lic";
                string filePath = Path.Combine(directoryPath, fileName);
                File.WriteAllText(filePath, vm.Model.LicenseValue ?? string.Empty, Encoding.UTF8);
            }
        }




        private bool _isRefreshing = false;
        public void GetCameraLicense()
        {
            if (_isRefreshing)
            {
                return;
            }
            _isRefreshing = true;
            Task.Run((Action)(() =>
            {
                int bufferLength = 1024;
                StringBuilder snBuilder = new StringBuilder(bufferLength);

                int ret = cvCameraCSLib.CM_GetAllCameraIDMD5(snBuilder, bufferLength);
                _isRefreshing = true;
                // 回到UI线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    log.Info($"GetAllCameraIDMD5 返回值: {ret}");
                    if (ret == 1)
                    {
                        string cameraIdsMd5 = snBuilder.ToString();
                        MessageBox1.Show(cameraIdsMd5, Properties.Resources.GetCameraLicense, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox1.Show(Properties.Resources.GetCameraIdMd5Failed, Properties.Resources.GetCameraLicense, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            }));
        }
        public int MyCallback(IntPtr strText, int nLen)
        {
            string text = Marshal.PtrToStringAnsi(strText, nLen);
            return 0;
        }

        public void GetSpectrumLicense()
        {
            IntPtr Handle = Spectrometer.CM_CreateEmission(0, MyCallback);
            int i = 0;

            int iR = Spectrometer.CM_Emission_Init(Handle, 0,9600 );
            int bufferLength = 1024;
            StringBuilder stringBuilder = new StringBuilder(bufferLength);
            cvColorVision.Spectrometer.CM_GetSpectrSerialNumber(Handle, stringBuilder);
            Spectrometer.CM_Emission_Close(Handle);
            Spectrometer.CM_ReleaseEmission(Handle);
            string sn = stringBuilder.ToString();
            if (string.IsNullOrWhiteSpace(sn))
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.NoDeviceDetected, Properties.Resources.Spectrometer);
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), stringBuilder.ToString(), Properties.Resources.Spectrometer);
            }
        }


        public void LoadLicenses()
        {
            Licenses.Clear();
            var licenses = PhyLicenseDao.Instance.GetAll();
            foreach (var license in licenses)
            {
                Licenses.Add(new LicenseViewModel(license));
            }

            OnPropertyChanged(nameof(LicenseCountText));
        }

        public void ImportLicense()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = true,
                Filter = "License files (*.lic;*.zip)|*.lic;*.zip|All files (*.*)|*.*",
                Title = Properties.Resources.LicenseImport,
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Use PhyCameraManager's Import logic
                PhyCameraManager.GetInstance().Import();
                LoadLicenses();
            }
        }

        public void ExportSelected()
        {
            if (SelectedLicense == null || string.IsNullOrEmpty(SelectedLicense.Model.LicenseValue))
                return;

            try
            {
                using var saveFileDialog = new System.Windows.Forms.SaveFileDialog
                {
                    Filter = "License files (*.lic)|*.lic|All files (*.*)|*.*",
                    Title = Properties.Resources.ExportLicenseToFile,
                    FileName = $"{SelectedLicense.Model.MacAddress}.lic",
                    RestoreDirectory = true
                };

                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    File.WriteAllText(saveFileDialog.FileName, SelectedLicense.Model.LicenseValue, Encoding.UTF8);
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.LicenseExportedSuccessfully, Properties.Resources.ExportLicense);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"{Properties.Resources.ExportLicenseFailed}: {ex.Message}", Properties.Resources.ExportLicense);
            }
        }

        public void DeleteSelected()
        {
            if (SelectedLicense == null)
                return;

            if (MessageBox.Show(Application.Current.GetActiveWindow(), 
                Properties.Resources.ConfirmDelete, 
                Properties.Resources.LicenseManager, 
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                PhyLicenseDao.Instance.DeleteById(SelectedLicense.Model.Id);
                LoadLicenses();
            }
        }

        public void CopyLicense()
        {
            if (SelectedLicense == null || string.IsNullOrEmpty(SelectedLicense.Model.LicenseValue))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.NoLicenseAvailable, Properties.Resources.CopyLicense);
                return;
            }

            try
            {
                Common.NativeMethods.Clipboard.SetText(SelectedLicense.Model.LicenseValue);
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.LicenseCopiedToClipboard, Properties.Resources.CopyLicense);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"{Properties.Resources.CopyLicenseFailed}: {ex.Message}", Properties.Resources.CopyLicense);
            }
        }
    }

    public class LicenseViewModel : ViewModelBase
    {
        public LicenseModel Model { get; set; }

        public LicenseViewModel(LicenseModel model)
        {
            Model = model;
        }

        public int Id => Model.Id;
        public string? MacAddress => Model.MacAddress;
        public string LiceTypeString => Model.LiceType == 0 ? Properties.Resources.LicenseTypeCamera : Model.LiceType == 1 ? Properties.Resources.LicenseTypeSpectrometer : Properties.Resources.LicenseTypeUnknown;
        public string? Model1 => this.Model.Model;
        public string? CusTomerName => this.Model.CusTomerName;
        public DateTime? ExpiryDate => this.Model.ExpiryDate;
        public DateTime? CreateDate => this.Model.CreateDate;

        public SolidColorBrush StatusColor
        {
            get
            {
                if (ExpiryDate == null || ExpiryDate < DateTime.Now)
                    return new SolidColorBrush(Colors.Red);
                if (ExpiryDate < DateTime.Now.AddDays(30))
                    return new SolidColorBrush(Colors.Yellow);
                return new SolidColorBrush(Colors.Green);
            }
        }

        public string StatusText
        {
            get
            {
                if (ExpiryDate == null || ExpiryDate < DateTime.Now)
                    return Properties.Resources.LicenseExpired;
                if (ExpiryDate < DateTime.Now.AddDays(30))
                    return Properties.Resources.LicenseExpiringSoon;
                return Properties.Resources.LicenseNormal;
            }
        }
    }

    public partial class LicenseManagerWindow : Window
    {
        public LicenseManagerViewModel ViewModel { get; set; }

        public LicenseManagerWindow()
        {
            ViewModel = new LicenseManagerViewModel();
            InitializeComponent();
            this.ApplyCaption();
            DataContext = ViewModel;
        }
    }
}
