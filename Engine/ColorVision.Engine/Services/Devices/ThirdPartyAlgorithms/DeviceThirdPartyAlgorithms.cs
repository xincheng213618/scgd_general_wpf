using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.Manager;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.Themes.Controls.Uploads;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    public class DeviceThirdPartyAlgorithms : DeviceService<ConfigThirdPartyAlgorithms>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceThirdPartyAlgorithms));

        public MQTTThirdPartyAlgorithms DService { get; set; }
        public ThirdPartyAlgorithmsView View { get; set; }
        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);


        [CommandDisplay("UploadThridPartPlusIn", CategoryOrder = 3)]
        [Category("MaintenanceDiagnostics")]
        [Description("CommandAlgorithmPluginHint")]
        public RelayCommand UploadPluginCommand { get; set; }
        [CommandDisplay("ThirdPartAlgorithmConfig", CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("CommandAlgorithmConfigHint")]
        public RelayCommand ThirdPartyAlgorithmsManagerCommand { get; set; }

        public DeviceThirdPartyAlgorithms(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTThirdPartyAlgorithms(this, Config);

            View = new ThirdPartyAlgorithmsView();
            this.SetIconResource("DrawingImageAlgorithm");

            DisplayAlgorithmControlLazy = new Lazy<DisplayThirdPartyAlgorithms>(() => { DisplayAlgorithmControl ??= new DisplayThirdPartyAlgorithms(this); return DisplayAlgorithmControl; });

            EditCommand = new RelayCommand(a =>
            {
                EditThirdPartyAlgorithms window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));

            UploadPluginCommand = new RelayCommand(a => UploadPlugin(), a => AccessControl.Check(PermissionMode.Administrator));

            ThirdPartyAlgorithmsManagerCommand = new RelayCommand(a => ThirdPartyAlgorithmsManager(), a => AccessControl.Check(PermissionMode.Administrator));
        }

        public SysResourceTpaDLLModel? DLLModel => SysResourceTpaDLLDao.Instance.GetByParam(new Dictionary<string, object>() { { "Code", Config.BindCode } });

        public  void ThirdPartyAlgorithmsManager()
        {
            var model = SysResourceTpaDLLDao.Instance.GetByParam(new Dictionary<string, object>() { { "Code", Config.BindCode } });
            if (model ==null)
            {
                MessageBox1.Show(Properties.Resources.ConfigureAssociatedDllFirst);
                return;
            }
            TemplateThirdPartyManager.Params.Clear();
            new TemplateEditorWindow(new TemplateThirdPartyManager() { DLLId = model.Id}) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public UploadMsgManager UploadMsgManager { get; set; } = new UploadMsgManager();

        public void UploadPlugin()
        {
            UploadWindow uploadwindow = new("插件(*.zip, *.dll,*.*)|*.zip;*.dll;*.*") { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += async (s, e) =>
            {
                await RunUploadAsync(async () =>
                {
                    UploadMsg uploadMsg = new UploadMsg(UploadMsgManager);
                    uploadMsg.Show();
                    await UploadPluginDataAsync(e.UploadFilePath);
                }, ex => log.Error("Third-party algorithm plugin upload failed.", ex));
            };
            uploadwindow.ShowDialog();
        }

        internal static async Task RunUploadAsync(Func<Task> uploadAsync, Action<Exception> onFailure)
        {
            ArgumentNullException.ThrowIfNull(uploadAsync);
            ArgumentNullException.ThrowIfNull(onFailure);

            try
            {
                await uploadAsync();
            }
            catch (Exception ex)
            {
                onFailure(ex);
            }
        }

        public async Task UploadPluginDataAsync(string path)
        {
            await InvokeOnApplicationDispatcherAsync(UploadMsgManager.UploadList.Clear);
            await Task.Delay(10);
            if (File.Exists(path))
            {
                FileUploadInfo uploadMeta = new FileUploadInfo();
                uploadMeta.FilePath = path;
                uploadMeta.FileName = Path.GetFileName(path);
                uploadMeta.FileSize = MemorySize.MemorySizeText(MemorySize.FileSize(path));
                uploadMeta.UploadStatus = UploadStatus.CheckingMD5;
                await InvokeOnApplicationDispatcherAsync(() => UploadMsgManager.UploadList.Add(uploadMeta));
                await Task.Delay(1);
                await InvokeOnApplicationDispatcherAsync(() => UploadMsgManager.Msg = Properties.Resources.CloseWindowInSeconds);
                await Task.Delay(1000);
                await InvokeOnApplicationDispatcherAsync(UploadMsgManager.Close);
            }
        }

        private static async Task InvokeOnApplicationDispatcherAsync(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return;

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            await dispatcher.InvokeAsync(action);
        }

        readonly Lazy<DisplayThirdPartyAlgorithms> DisplayAlgorithmControlLazy;
        public DisplayThirdPartyAlgorithms DisplayAlgorithmControl { get; set; }


        public override UserControl GetDeviceInfo() => new InfoThirdPartyAlgorithms(this);

        public override UserControl GetDisplayControl() => DisplayAlgorithmControlLazy.Value;


        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
