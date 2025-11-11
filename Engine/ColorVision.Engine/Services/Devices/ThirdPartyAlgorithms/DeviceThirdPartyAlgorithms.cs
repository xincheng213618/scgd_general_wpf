using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.Manager;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI.Authorizations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Themes.Controls.Uploads;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views;
using System.ComponentModel;
using ColorVision.Database;
using ColorVision.Engine.Extension;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    public class DeviceThirdPartyAlgorithms : DeviceService<ConfigThirdPartyAlgorithms>
    {
        public MQTTThirdPartyAlgorithms DService { get; set; }
        public ThirdPartyAlgorithmsView View { get; set; }
        public IDisPlayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayCameraConfig<IDisPlayConfigBase>(Config.Code);


        [CommandDisplay("UploadThridPartPlusIn")]
        public RelayCommand UploadPluginCommand { get; set; }
        [CommandDisplay("ThirdPartAlgorithmConfig")]
        public RelayCommand ThirdPartyAlgorithmsManagerCommand { get; set; }

        public DeviceThirdPartyAlgorithms(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTThirdPartyAlgorithms(this, Config);

            View = new ThirdPartyAlgorithmsView();
            View.View.Title = ColorVision.Engine.Properties.Resources.ThirdPartAlgView+$" - {Config.Code}";
            this.SetIconResource("DrawingImageAlgorithm", View.View);

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
                MessageBox1.Show("请先在配置中配置关联的dll");
                return;
            }
            TemplateThirdPartyManager.Params.Clear();
            new TemplateEditorWindow(new TemplateThirdPartyManager() { DLLId = model.Id}) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public UploadMsgManager UploadMsgManager { get; set; } = new UploadMsgManager();

        public void UploadPlugin()
        {
            UploadWindow uploadwindow = new("插件(*.zip, *.dll,*.*)|*.zip;*.dll;*.*") { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                UploadMsg uploadMsg = new UploadMsg(UploadMsgManager);
                uploadMsg.Show();
                string uploadfilepath = e.UploadFilePath;
                Task.Run(() => UploadPluginData(uploadfilepath));
            };
            uploadwindow.ShowDialog();
        }

        public async void UploadPluginData(string path)
        {
            Application.Current.Dispatcher.Invoke(UploadMsgManager.UploadList.Clear);
            await Task.Delay(10);
            if (File.Exists(path))
            {
                FileUploadInfo uploadMeta = new FileUploadInfo();
                uploadMeta.FilePath = path;
                uploadMeta.FileName = Path.GetFileName(path);
                uploadMeta.FileSize = MemorySize.MemorySizeText(MemorySize.FileSize(path));
                uploadMeta.UploadStatus = UploadStatus.CheckingMD5;
                Application.Current.Dispatcher.Invoke(()=> UploadMsgManager.UploadList.Add(uploadMeta));
                ;
                await Task.Delay(1);
                var msgRecord = await DService.UploadCalibrationFileAsync(SysResourceModel.Code ?? Name, uploadMeta.FileName, uploadMeta.FilePath, 3);
                if (msgRecord != null && msgRecord.MsgRecordState == MsgRecordState.Success)
                {
                    uploadMeta.UploadStatus = UploadStatus.Completed;
                    string FileName = msgRecord.MsgReturn.Data.FileName;

                }
                else
                {
                    uploadMeta.UploadStatus = UploadStatus.Failed;
                }
                UploadMsgManager.Msg = "1s 后关闭窗口";
                await Task.Delay(1000);
                UploadMsgManager.Close();
            }

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
