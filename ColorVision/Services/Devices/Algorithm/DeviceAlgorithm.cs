using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Interfaces;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Views;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Algorithm
{
    public class DeviceAlgorithm : DeviceService<ConfigAlgorithm>
    {
        public MQTTAlgorithm MQTTService { get; set; }
        public AlgorithmView View { get; set; }

        public DeviceAlgorithm(SysDeviceModel sysResourceModel) : base(sysResourceModel)
        {
            MQTTService = new MQTTAlgorithm(this, Config);

            View = new AlgorithmView(this);
            View.View.Title = $"算法视图 - {Config.Code}";
            this.SetIconResource("DrawingImageAlgorithm", View.View);

            DisplayAlgorithmControlLazy = new Lazy<DisplayAlgorithmControl>(() => { DisplayAlgorithmControl ??= new DisplayAlgorithmControl(this); return DisplayAlgorithmControl; });

            EditCommand = new RelayCommand(a =>
            {
                EditAlgorithm window = new EditAlgorithm(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });
        }

        readonly Lazy<DisplayAlgorithmControl> DisplayAlgorithmControlLazy;
        public DisplayAlgorithmControl DisplayAlgorithmControl { get; set; }

        public override UserControl GetDeviceControl() => new DeviceAlgorithmControl(this);
        public override UserControl GetDeviceInfo() => new DeviceAlgorithmControl(this, false);

        public override UserControl GetDisplayControl() => DisplayAlgorithmControlLazy.Value;


        public override MQTTServiceBase? GetMQTTService()
        {
            return MQTTService;
        }
    }
}
