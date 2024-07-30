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
using ColorVision.Themes.Controls;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.PhyCameras.Group;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    public class DeviceThirdPartyAlgorithms : DeviceService<ConfigThirdPartyAlgorithms>
    {
        public MQTTThirdPartyAlgorithms DService { get; set; }
        public AlgorithmView View { get; set; }
        public RelayCommand UploadPluginCommand { get; set; }

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

            UploadPluginCommand = new RelayCommand(a => UploadPlugin(), a => AccessControl.Check(PermissionMode.Administrator));
        }

        public UploadMsgManager UploadMsgManager { get; set; } = new UploadMsgManager();

        public void UploadPlugin()
        {
            UploadWindow uploadwindow = new("插件(*.zip, *.dll,*.*)|*.zip;*.dll;*.*") { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            uploadwindow.OnUpload += (s, e) =>
            {
                if (s is Upload upload)
                {
                    UploadMsg uploadMsg = new UploadMsg(UploadMsgManager);
                    uploadMsg.Show();
                    string path = upload.UploadFilePath;
                    Task.Run(() => UploadPluginData(path));
                }
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
                var msgRecord = await RCFileUpload.GetInstance().UploadCalibrationFileAsync(SysResourceModel.Code ?? Name, uploadMeta.FileName, uploadMeta.FilePath, 3);
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
