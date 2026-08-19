using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using ColorVision.UI.Views;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class DeviceAlgorithm : DeviceService<ConfigAlgorithm>
    {
        private bool _isDisposed;

        public MQTTAlgorithm DService { get; set; }
        private readonly Lazy<AlgorithmView> _view;
        public AlgorithmView View => _view.Value;

        internal bool IsDisposed => _isDisposed;

        public DisplayAlgorithmConfig DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<DisplayAlgorithmConfig>(Config.Code);

        public DeviceAlgorithm(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTAlgorithm(Config);
            _view = new Lazy<AlgorithmView>(() => Application.Current.Dispatcher.CheckAccess()
                ? new AlgorithmView(this)
                : Application.Current.Dispatcher.Invoke(() => new AlgorithmView(this)));
            this.SetIconResource("DrawingImageAlgorithm");

            DisplayAlgorithmControlLazy = new Lazy<DisplayAlgorithm>(() => { DisplayAlgorithm ??= new DisplayAlgorithm(this); return DisplayAlgorithm; });

            EditCommand = new RelayCommand(a =>
            {
                var propertyEditorWindow = new PropertyEditorWindow(Config, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submitted += (s, e) => Save();
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

        public override void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (DisplayAlgorithmControlLazy.IsValueCreated)
                DisplayAlgorithmControlLazy.Value.Dispose();

            if (_view.IsValueCreated)
            {
                DockViewManager.GetInstance().RemoveView(_view.Value);
                _view.Value.Dispose();
            }

            DService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
