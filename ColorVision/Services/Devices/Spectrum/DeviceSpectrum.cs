using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum.Views;
using System.Windows.Controls;
using ColorVision.Services.Dao;
using ColorVision.Themes.Controls;
using ColorVision.MVVM;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Devices.Camera;
using System.Windows;
using ColorVision.Services.Interfaces;
using ColorVision.Services.Msg;
using System.Collections.Generic;
using System.Security.Cryptography;
using ColorVision.Common.Utilities;
using ColorVision.Templates;
using System.Collections.ObjectModel;
using ColorVision.MySql.Service;

namespace ColorVision.Services.Devices.Spectrum
{
    public class DeviceSpectrum : DeviceService<ConfigSpectrum>, IUploadMsg
    {
        public MQTTSpectrum DeviceService { get; set; }

        public ViewSpectrum View { get; set; }
        public RelayCommand UploadSpectrumCommand { get; set; }
        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTSpectrum(Config);
            View = new ViewSpectrum(this);
            UploadSpectrumCommand = new RelayCommand(UploadResource);
            SpectrumResourceParam.Load(SpectrumResourceParams, SysResourceModel.Id, ModMasterType.SpectrumResource);
        }

        public string Msg { get => _Msg; set { _Msg = value; Application.Current.Dispatcher.Invoke(() => NotifyPropertyChanged()); } }
        private string _Msg;
        public void UploadResource(object sender)
        {
            UploadWindow uploadwindow = new UploadWindow() { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                if (s is Upload upload)
                {

                    string FileName = upload.UploadFilePath;
                    string md5 = Tool.CalculateMD5(FileName);


                    SysResourceDao sysResourceDao = new SysResourceDao();
                    SysResourceModel sysResourceModel = new SysResourceModel();
                    sysResourceModel.Name = upload.UploadFileName;
                    sysResourceModel.Code = md5;
                    sysResourceModel.Type = 201;
                    sysResourceModel.Pid = this.SysResourceModel.Id;
                    sysResourceModel.Value = FileName;
                    sysResourceDao.Save(sysResourceModel);
                    if (sysResourceModel != null)
                    {
                        BaseResource calibrationResource = new BaseResource(sysResourceModel);
                        this.AddChild(calibrationResource);   
                    }

                    //UploadMsg uploadMsg = new UploadMsg(this);
                    //UploadCalibrationClosed += (s, e) =>
                    //{
                    //    uploadMsg.Close();
                    //};
                    //uploadMsg.Show();
                    //string path = upload.UploadFilePath;
                    //Task.Run(() => UploadData(path));


                }
            };
            uploadwindow.ShowDialog();
        }



        public override UserControl GetDeviceControl() => new DeviceSpectrumControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSpectrumControl(this, false);
        public override UserControl GetDisplayControl() => new DisplaySpectrumControl(this);
        public override UserControl GetEditControl() => new EditSpectrum(this);

    }
}
