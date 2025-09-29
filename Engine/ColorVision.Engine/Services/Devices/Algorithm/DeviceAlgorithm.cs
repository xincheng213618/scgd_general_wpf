using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class DeviceAlgorithm : DeviceService<ConfigAlgorithm>
    {
        public MQTTAlgorithm DService { get; set; }
        public AlgorithmView View { get; set; }

        public DeviceAlgorithm(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTAlgorithm(this, Config);

            View = new AlgorithmView();
            View.View.Title = $"算法视图 - {Config.Code}";
            this.SetIconResource("DrawingImageAlgorithm", View.View);

            DisplayAlgorithmControlLazy = new Lazy<DisplayAlgorithm>(() => { DisplayAlgorithm ??= new DisplayAlgorithm(this); return DisplayAlgorithm; });

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => Save();
                propertyEditorWindow.ShowDialog();

            }, a => AccessControl.Check(PermissionMode.Administrator));
        }

        private void Con_Submited(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        readonly Lazy<DisplayAlgorithm> DisplayAlgorithmControlLazy;
        public DisplayAlgorithm DisplayAlgorithm { get; set; }

        public override UserControl GetDeviceInfo() => new InfoAlgorithm(this);

        public override UserControl GetDisplayControl() => DisplayAlgorithmControlLazy.Value;
        public override MQTTServiceBase? GetMQTTService() => DService;
    }
}
