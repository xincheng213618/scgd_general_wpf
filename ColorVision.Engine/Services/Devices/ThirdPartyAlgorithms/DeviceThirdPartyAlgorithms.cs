using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Util.Interfaces;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    public class DeviceThirdPartyAlgorithms : DeviceService<ConfigThirdPartyAlgorithms>
    {
        public MQTTThirdPartyAlgorithms DService { get; set; }
        public AlgorithmView View { get; set; }

        public DeviceThirdPartyAlgorithms(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTThirdPartyAlgorithms(this, Config);

            View = new AlgorithmView();
            View.View.Title = $"第三方算法视图 - {Config.Code}";
            this.SetIconResource("DrawingImageAlgorithm", View.View);

            DisplayAlgorithmControlLazy = new Lazy<DisplayThirdPartyAlgorithms>(() => { DisplayAlgorithmControl ??= new DisplayThirdPartyAlgorithms(this); return DisplayAlgorithmControl; });

            EditCommand = new RelayCommand(a =>
            {
                EditThirdPartyAlgorithms window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        readonly Lazy<DisplayThirdPartyAlgorithms> DisplayAlgorithmControlLazy;
        public DisplayThirdPartyAlgorithms DisplayAlgorithmControl { get; set; }


        public override UserControl GetDeviceControl() => new InfoThirdPartyAlgorithms(this);
        public override UserControl GetDeviceInfo() => new InfoThirdPartyAlgorithms(this);

        public override UserControl GetDisplayControl() => DisplayAlgorithmControlLazy.Value;


        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
