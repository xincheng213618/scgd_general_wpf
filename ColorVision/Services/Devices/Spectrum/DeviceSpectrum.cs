using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum.Views;
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
            UploadSpectrumCommand = new RelayCommand(UploadResource);
            SpectrumResourceParam.Load(SpectrumResourceParams, SysResourceModel.Id, ModMasterType.SpectrumResource);
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
            SysResourceDao sysResourceDao = new SysResourceDao();
            SysResourceModel sysResourceModel = new SysResourceModel();
            sysResourceModel.Name = UploadFileName;
            sysResourceModel.Code = md5;
            sysResourceModel.Type = 201;
            sysResourceModel.Pid = this.SysResourceModel.Id;
            sysResourceModel.Value = Path.GetFileName(UploadFilePath);
            sysResourceDao.Save(sysResourceModel);
            if (sysResourceModel != null)
            {
                BaseResource calibrationResource = new BaseResource(sysResourceModel);
                this.AddChild(calibrationResource);
            }

            Application.Current.Dispatcher.Invoke(() => UploadClosed.Invoke(this, new EventArgs()));
        }


        public override UserControl GetDeviceControl() => new DeviceSpectrumControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSpectrumControl(this, false);
        public override UserControl GetDisplayControl() => new DisplaySpectrumControl(this);
        public override UserControl GetEditControl() => new EditSpectrum(this);
        public override MQTTServiceBase? GetMQTTService()
        {
            return DeviceService;
        }
    }
}
