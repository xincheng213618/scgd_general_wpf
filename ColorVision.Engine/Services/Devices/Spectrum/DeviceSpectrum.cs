using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI.Authorizations;
using ColorVision.Util.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public class DeviceSpectrum : DeviceService<ConfigSpectrum>, IUploadMsg
    {
        public MQTTSpectrum DService { get; set; }

        public ViewSpectrum View { get; set; }
        public RelayCommand UploadSpectrumCommand { get; set; }
        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();

        public DeviceSpectrum(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSpectrum(Config);
            View = new ViewSpectrum(this);
            View.View.Title = $"光谱仪视图 - {Config.Code}";
            this.SetIconResource("DISpectrumIcon", View.View);

            UploadSpectrumCommand = new RelayCommand(UploadResource);
            SpectrumResourceParam.Load(SpectrumResourceParams, SysResourceModel.Id, ModMasterType.SpectrumResource);

            EditCommand = new RelayCommand(a =>
            {
                EditSpectrum window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));

            DisplayLazy = new Lazy<DisplaySpectrumControl>(() => new DisplaySpectrumControl(this));

            ResourceManagerCommand = new RelayCommand(a =>
            {
                SpectrumResourceControl calibration = SpectrumResourceParams.Count == 0 ? new SpectrumResourceControl(this) : new SpectrumResourceControl(this, this.SpectrumResourceParams[0].Value);
                var ITemplate = new TemplateSpectrumResourceParam() {  Device =this,TemplateParams = this.SpectrumResourceParams, SpectrumResourceControl = calibration, Title = "SpectrumResourceParams" };

                WindowTemplate windowTemplate = new(ITemplate);
                windowTemplate.Owner = Application.Current.GetActiveWindow();
                windowTemplate.ShowDialog();
            });
        }
        public string Msg { get => _Msg; set { _Msg = value; Application.Current.Dispatcher.Invoke(() => NotifyPropertyChanged()); } }
        public ObservableCollection<FileUploadInfo> UploadList { get; set; }
        private string _Msg;
        public event EventHandler UploadClosed;

        public void UploadResource(object sender)
        {
            UploadWindow uploadwindow = new() { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                if (s is Upload upload)
                {
                    UploadMsg uploadMsg = new(this);
                    uploadMsg.Show();
                    string name = upload.UploadFileName;
                    string path = upload.UploadFilePath;
                    Task.Run(() => UploadData(name, path));
                }
            };
            uploadwindow.ShowDialog();
        }
        public async void UploadData(string UploadFileName, string UploadFilePath)
        {
            Msg = "正在解压文件：" + " 请稍后...";
            await Task.Delay(10);

            string md5 = Tool.CalculateMD5(UploadFilePath);
            var msgRecord = await RCFileUpload.GetInstance().UploadCalibrationFileAsync(Code, UploadFileName, UploadFilePath, 201);
            SysResourceModel sysResourceModel = new();
            sysResourceModel.Name = UploadFileName;
            sysResourceModel.Code = md5;
            sysResourceModel.Type = 201;
            sysResourceModel.Pid = SysResourceModel.Id;
            sysResourceModel.Value = Path.GetFileName(UploadFilePath);
            SysResourceDao.Instance.Save(sysResourceModel);
            if (sysResourceModel != null)
            {
                BaseFileResource calibrationResource = new(sysResourceModel);
                AddChild(calibrationResource);
            }

            Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
        }


        public override UserControl GetDeviceControl() => new InfoSpectrum(this);
        public override UserControl GetDeviceInfo() => new InfoSpectrum(this);

        readonly Lazy<DisplaySpectrumControl> DisplayLazy;
        public override UserControl GetDisplayControl() => DisplayLazy.Value;
        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
