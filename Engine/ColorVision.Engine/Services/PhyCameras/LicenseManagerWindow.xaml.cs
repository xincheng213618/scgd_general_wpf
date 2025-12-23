using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Menus;
using cvColorVision;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// Menu item to open License Manager
    /// </summary>
    public class ExportLicenseManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Properties.Resources.LicenseManager;
        public override int Order => 3;

        public override void Execute()
        {
            new LicenseManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
        }
    }

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

        public LicenseManagerViewModel()
        {
            RefreshCommand = new RelayCommand(a => LoadLicenses());
            ImportCommand = new RelayCommand(a => ImportLicense());
            ExportSelectedCommand = new RelayCommand(a => ExportSelected(), a => SelectedLicense != null);
            DeleteSelectedCommand = new RelayCommand(a => DeleteSelected(), a => SelectedLicense != null);
            CopyLicenseCommand = new RelayCommand(a => CopyLicense(), a => SelectedLicense != null);

            LoadLicenses();
        }
        private bool _isRefreshing = false;
        public void GetCameraLicense()
        {
            if (_isRefreshing)
            {
                return;
            }
            _isRefreshing = true;
            Task.Run(() =>
            {
                int bufferLength = 1024;
                StringBuilder snBuilder = new StringBuilder(bufferLength);

                int ret = cvCameraCSLib.GetAllCameraIDMD5(snBuilder, bufferLength);
                _isRefreshing = true;
                // 回到UI线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    log.Info($"GetAllCameraIDMD5 返回值: {ret}");
                    if (ret == 1)
                    {
                        string cameraIdsMd5 = snBuilder.ToString();
                        MessageBox1.Show(cameraIdsMd5, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox1.Show("获取相机ID MD5失败", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
            });
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
                MessageBox1.Show(Application.Current.GetActiveWindow(), "No Device", "Sprectrum");
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), stringBuilder.ToString(), "Sprectrum");
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
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.LicenseExportedSuccessfully, "ColorVision");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"{Properties.Resources.ExportLicenseFailed}: {ex.Message}", "ColorVision");
            }
        }

        public void DeleteSelected()
        {
            if (SelectedLicense == null)
                return;

            if (MessageBox.Show(Application.Current.GetActiveWindow(), 
                Properties.Resources.ConfirmDelete, 
                "ColorVision", 
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
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.NoLicenseAvailable, "ColorVision");
                return;
            }

            try
            {
                Common.NativeMethods.Clipboard.SetText(SelectedLicense.Model.LicenseValue);
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.LicenseCopiedToClipboard, "ColorVision");
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"{Properties.Resources.CopyLicenseFailed}: {ex.Message}", "ColorVision");
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
        public string LiceTypeString => Model.LiceType == 0 ? "相机" : Model.LiceType == 1 ? "光谱仪" : "未知";
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
                    return "已过期";
                if (ExpiryDate < DateTime.Now.AddDays(30))
                    return "即将过期";
                return "正常";
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
