#pragma warning disable CS8601,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Extension;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public class DisplaySpectrumConfig : ViewModelBase
    {
        public int PortNum { get => _PortNum; set { _PortNum = value; OnPropertyChanged(); } }
        private int _PortNum = 1;
    }

    public class DeviceSpectrum : DeviceService<ConfigSpectrum>
    {
        public DisplaySpectrumConfig DisplaySpectrumConfig { get; set; } = new DisplaySpectrumConfig();
        public MQTTSpectrum DService { get; set; }
        public ViewSpectrum View { get; set; }
        public IDisPlayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisPlayConfigBase>(Config.Code);

        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();
        public RelayCommand RefreshDeviceIdCommand { get; set; }

        [CommandDisplay("UploadLic")]
        public RelayCommand UploadLincenseCommand { get; set; }

        [CommandDisplay("AdaptiveZeroCalibration")]

        public RelayCommand SelfAdaptionInitDarkCommand { get; set; }

        [CommandDisplay("ApaptivezeroCaliSet")]
        public RelayCommand SelfAdaptionInitDarkSettingCommand { get; set; }

        [CommandDisplay("EmissionSP100Set")]
        public RelayCommand EmissionSP100SettingCommand { get; set; }
        public event Action SelfAdaptionInitDarkStarted;
        public event Action SelfAdaptionInitDarkCompleted;

        [CommandDisplay("获取当前SN")]
        public RelayCommand GetSpectrSerialNumberCommand { get; set; }
        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSpectrum(this);
            View = new ViewSpectrum();
            View.View.Title = ColorVision.Engine.Properties.Resources.SpectrumView+$" - {Config.Code}";
            this.SetIconResource("DISpectrumIcon", View.View);

            SpectrumResourceParam.Load(SpectrumResourceParams, SysResourceModel.Id);

            EditCommand = new RelayCommand(a =>
            {
                PropertyEditorWindow window = new PropertyEditorWindow(Config);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.Submited +=(s,e)=>
                {
                    Save();
                };
                window.ShowDialog();

            }, a => AccessControl.Check(PermissionMode.Administrator));

            DisplayLazy = new Lazy<DisplaySpectrum>(() => new DisplaySpectrum(this));

            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());
            UploadLincenseCommand = new RelayCommand(a => UploadLincense());

            SelfAdaptionInitDarkCommand = new RelayCommand(a => SelfAdaptionInitDark());
            SelfAdaptionInitDarkSettingCommand = new RelayCommand(a => SelfAdaptionInitDarkSetting());
            EmissionSP100SettingCommand = new RelayCommand(a => EmissionSP100Setting());

            GetSpectrSerialNumberCommand = new RelayCommand(a => GetSpectrSerialNumber());

        }
        public int MyCallback(IntPtr strText, int nLen)
        {
            string text = Marshal.PtrToStringAnsi(strText, nLen);
            return 0;
        }

        public void GetSpectrSerialNumber()
        {
            IntPtr Handle = Spectrometer.CM_CreateEmission((int)Config.SpectrometerType, MyCallback);
            int i = 0;
            if (int.TryParse(Config.ComPort, out int z))
            {
                i = z;
            }
            int iR = Spectrometer.CM_Emission_Init(Handle, i, Config.BaudRate);
            int bufferLength = 1024;
            StringBuilder stringBuilder = new StringBuilder(bufferLength);
            cvColorVision.Spectrometer.CM_GetSpectrSerialNumber(Handle,stringBuilder);
            Spectrometer.CM_Emission_Close(Handle);
            Spectrometer.CM_ReleaseEmission(Handle);
            string sn = stringBuilder.ToString();
            if (string.IsNullOrWhiteSpace(sn))
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "No Device", "Sprectrum");
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(),stringBuilder.ToString(),"Sprectrum");
            }
        }

        public void SelfAdaptionInitDark()
        {
            MsgRecord msgRecord = DService.SelfAdaptionInitDark();
            SelfAdaptionInitDarkStarted?.Invoke();
            msgRecord.MsgRecordStateChanged +=(e) =>
            {
                SelfAdaptionInitDarkCompleted?.Invoke();
                if (msgRecord.MsgReturn != null)
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExcAdaptiveZeroCali + e.ToString(), "ColorVison");
            };
        }

        public void SelfAdaptionInitDarkSetting()
        {
            new PropertyEditorWindow(Config.SelfAdaptionInitDark) { Owner =Application.Current.GetActiveWindow() ,WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            SaveConfig();
        }
        public void EmissionSP100Setting()
        {
            new PropertyEditorWindow(Config.SetEmissionSP100Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            SaveConfig();
        }

        public void UploadLincense()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            openFileDialog.Multiselect = true; // 允许多选
            openFileDialog.Filter = "All files (*.*)|*.zip;*.lic"; // 可以设置特定的文件类型过滤器
            openFileDialog.Title = ColorVision.Engine.Properties.Resources.SelectLicenseFilePrompt  + SysResourceModel.Code;
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

        public async Task UploadLicenseNet(string sn)
        {
            // 设置请求的URL和数据
            string url = "https://color-vision.picp.net/license/api/v1/license/onlyDownloadLicense";
            var postData = new { macSn = sn };
            string DirLicense = $"{Environments.DirAppData}\\Licenses";
            if (!Directory.Exists(DirLicense))
                Directory.CreateDirectory(DirLicense);

            string fileName = $"{DirLicense}\\{sn}-license.zip";

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
                catch 
                {

                }
            }
        }
        public  LicenseModel CameraLicenseModel { get; set; }
        public void SetLicense(string filepath)
        {
            if (!File.Exists(filepath)) return;
            if (Path.GetExtension(filepath) == ".zip")
            {
                try
                {
                    using ZipArchive archive = ZipFile.OpenRead(filepath);
                    var licFiles = archive.Entries.Where(entry => Path.GetExtension(entry.FullName).Equals(".lic", StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var item in licFiles)
                    {
                        string Code = Path.GetFileNameWithoutExtension(item.FullName);
                        CameraLicenseModel = CameraLicenseDao.Instance.GetByMAC(Code);
                        if (CameraLicenseModel == null)
                            CameraLicenseModel = new LicenseModel();
                        CameraLicenseModel.DevCameraId = SysResourceModel.Id;
                        CameraLicenseModel.LiceType = 1;
                        CameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(item.FullName);
                        using var stream = item.Open();
                        using var reader = new StreamReader(stream, Encoding.UTF8); // 假设文件编码为UTF-8
                        CameraLicenseModel.LicenseValue = reader.ReadToEnd();
                        CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLicense.Licensee;
                        CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLicense.DeviceMode;
                        CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLicense.ExpiryDateTime;
                        int ret = CameraLicenseDao.Instance.Save(CameraLicenseModel);
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? ColorVision.Engine.Properties.Resources.AddFailed : ColorVision.Engine.Properties.Resources.AddSuccess)}", "ColorVision");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExtractionFailed+$" :{ex.Message}", "ColorVision");
                }
            }
            else if (Path.GetExtension(filepath) == ".lic")
            {
                string Code = Path.GetFileNameWithoutExtension(filepath);
                CameraLicenseModel = CameraLicenseDao.Instance.GetByMAC(Code);
                if (CameraLicenseModel == null)
                    CameraLicenseModel = new LicenseModel();
                CameraLicenseModel.MacAddress = Path.GetFileNameWithoutExtension(filepath);
                CameraLicenseModel.LiceType = 1;
                CameraLicenseModel.LicenseValue = File.ReadAllText(filepath);
                CameraLicenseModel.CusTomerName = CameraLicenseModel.ColorVisionLicense.Licensee;
                CameraLicenseModel.Model = CameraLicenseModel.ColorVisionLicense.DeviceMode;
                CameraLicenseModel.ExpiryDate = CameraLicenseModel.ColorVisionLicense.ExpiryDateTime;

                int ret = CameraLicenseDao.Instance.Save(CameraLicenseModel);
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? ColorVision.Engine.Properties.Resources.AddFailed : ColorVision.Engine.Properties.Resources.UpdataSucess)}", "ColorVision");
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.UnsupportedLicenseFileExtension, "ColorVision");
            }
        }

        public void RefreshDeviceId()
        {
            MsgRecord msgRecord = DService.GetAllSnID();
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (msgRecord.MsgReturn != null)
                {
                    List<string> strings = new List<string>();
                    foreach (var item in SysResourceDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "type", 103 } }))
                    {
                        strings.Add(item.Code);
                        Task.Run(() => UploadLicenseNet(item.Code));
                    }
                    string result = string.Join(",", strings);
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.AllSpectrumDeviceInfo + Environment.NewLine + result);
                }
                RefreshEmptySpectrum();
            };

        }
        public void RefreshEmptySpectrum()
        {
             Count = SysResourceDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "type", 103 } }).Where(a => string.IsNullOrWhiteSpace(a.Value)).ToList().Count;
        }

        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count;

        public override UserControl GetDeviceInfo() => new InfoSpectrum(this);

        readonly Lazy<DisplaySpectrum> DisplayLazy;
        public override UserControl GetDisplayControl() => DisplayLazy.Value;
        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
