using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum.Views;
using ColorVision.Services.Extension;
using ColorVision.Services.Templates;
using ColorVision.Themes.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Spectrum
{
    public class DeviceSpectrum : DeviceService<ConfigSpectrum>, IUploadMsg
    {
        public MQTTSpectrum DeviceService { get; set; }

        public ViewSpectrum View { get; set; }
        public RelayCommand UploadSpectrumCommand { get; set; }
        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();

        public DeviceSpectrum(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTSpectrum(Config);
            View = new ViewSpectrum(this);
            View.View.Title = $"光谱仪视图 - {Config.Code}";
            this.SetIconResource("DISpectrumIcon", View.View);

            UploadSpectrumCommand = new RelayCommand(UploadResource);
            SpectrumResourceParam.Load(SpectrumResourceParams, SysResourceModel.Id, ModMasterType.SpectrumResource);

            EditCommand = new RelayCommand(a =>
            {
                EditSpectrum window = new EditSpectrum(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });
            DisplayLazy = new Lazy<DisplaySpectrumControl>(() => new DisplaySpectrumControl(this));
        }

        public string Msg { get => _Msg; set { _Msg = value; Application.Current.Dispatcher.Invoke(() => NotifyPropertyChanged()); } }
        private string _Msg;

        public event EventHandler UploadClosed;

        public void UploadResource(object sender)
        {
            UploadWindow uploadwindow = new UploadWindow() { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                if (s is Upload upload)
                {
                    UploadMsg uploadMsg = new UploadMsg(this);
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
            var msgRecord = await DeviceService.UploadFileAsync(UploadFileName, UploadFilePath,201);
            SysResourceModel sysResourceModel = new SysResourceModel();
            sysResourceModel.Name = UploadFileName;
            sysResourceModel.Code = md5;
            sysResourceModel.Type = 201;
            sysResourceModel.Pid = SysResourceModel.Id;
            sysResourceModel.Value = Path.GetFileName(UploadFilePath);
            SysResourceDao.Instance.Save(sysResourceModel);
            if (sysResourceModel != null)
            {
                BaseResource calibrationResource = new BaseResource(sysResourceModel);
                AddChild(calibrationResource);
            }

            Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
        }


        public override UserControl GetDeviceControl() => new DeviceSpectrumControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSpectrumControl(this, false);

        readonly Lazy<DisplaySpectrumControl> DisplayLazy;
        public override UserControl GetDisplayControl() => DisplayLazy.Value;
        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
