#pragma warning disable CS8601,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.PhyCameras.Dao;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
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
using ColorVision.Database;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public class DeviceSpectrum : DeviceService<ConfigSpectrum>
    {
        public MQTTSpectrum DService { get; set; }
        public ViewSpectrum View { get; set; }
        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();
        public RelayCommand RefreshDeviceIdCommand { get; set; }

        [CommandDisplay("上传许可证")]
        public RelayCommand UploadLincenseCommand { get; set; }

        [CommandDisplay("自适应校零")]

        public RelayCommand SelfAdaptionInitDarkCommand { get; set; }

        [CommandDisplay("自适应校零设置")]
        public RelayCommand SelfAdaptionInitDarkSettingCommand { get; set; }

        [CommandDisplay("EmissionSP100设置")]
        public RelayCommand EmissionSP100SettingCommand { get; set; }


        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSpectrum(this);
            View = new ViewSpectrum();
            View.View.Title = $"光谱仪视图 - {Config.Code}";
            this.SetIconResource("DISpectrumIcon", View.View);

            SpectrumResourceParam.Load(SpectrumResourceParams, SysResourceModel.Id);

            EditCommand = new RelayCommand(a =>
            {
                EditSpectrum window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));

            DisplayLazy = new Lazy<DisplaySpectrum>(() => new DisplaySpectrum(this));

            RefreshDeviceIdCommand = new RelayCommand(a => RefreshDeviceId());
            UploadLincenseCommand = new RelayCommand(a => UploadLincense());

            SelfAdaptionInitDarkCommand = new RelayCommand(a => SelfAdaptionInitDark());
            SelfAdaptionInitDarkSettingCommand = new RelayCommand(a => SelfAdaptionInitDarkSetting());
            EmissionSP100SettingCommand = new RelayCommand(a => EmissionSP100Setting());
        }

        public void SelfAdaptionInitDark()
        {
            MsgRecord msgRecord = DService.SelfAdaptionInitDark();
            msgRecord.MsgRecordStateChanged +=(e) =>
            {
                if (msgRecord.MsgReturn != null)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(),"自适应校零执行" + e.ToString(),"ColorVison");
                }
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
                        MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "添加成功")}", "ColorVision");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), $"解压失败 :{ex.Message}", "ColorVision");
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
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{CameraLicenseModel.MacAddress} {(ret == -1 ? "添加失败" : "更新成功")}", "ColorVision");
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "不支持的许可文件后缀", "ColorVision");
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
                    MessageBox.Show(Application.Current.GetActiveWindow(), "所有光谱仪设备信息" + Environment.NewLine + result);
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
