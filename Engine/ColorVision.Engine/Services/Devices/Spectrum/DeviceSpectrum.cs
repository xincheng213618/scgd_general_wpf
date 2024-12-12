using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls.Uploads;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public class DeviceSpectrum : DeviceService<ConfigSpectrum>
    {
        public MQTTSpectrum DService { get; set; }
        public ViewSpectrum View { get; set; }
        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; } = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();
        public RelayCommand RefreshDeviceIdCommand { get; set; }


        public DeviceSpectrum(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSpectrum(this);
            View = new ViewSpectrum(this);
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
        }

        public void RefreshDeviceId()
        {
            MsgRecord msgRecord = DService.GetAllCameraID();
            msgRecord.MsgSucessed += (e) =>
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "当前设备信息" + Environment.NewLine + msgRecord.MsgReturn.Data);
                RefreshEmptySpectrum();
            };
        }
        public void RefreshEmptySpectrum()
        {
             Count = SysResourceDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "type", 103 } }).Where(a => string.IsNullOrWhiteSpace(a.Value)).ToList().Count;
        }

        public int Count { get => _Count; set { _Count = value; NotifyPropertyChanged(); } }
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
