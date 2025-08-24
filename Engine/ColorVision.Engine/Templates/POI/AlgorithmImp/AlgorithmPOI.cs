using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIRevise;
using FlowEngineLib.Algorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{

    [DisplayAlgorithm(1, "POI", "数据提取算法")]
    public class AlgorithmPoi : DisplayAlgorithmBase
    {

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public RelayCommand OpenTemplatePOIFilterCommand { get; set; }

        public RelayCommand OpenTemplatePoiReviseCommand { get; set; }

        public RelayCommand OpenTemplatePoiOutputCommand { get; set; }
        public RelayCommand OpenPoiFileCommand { get; set; }


        public AlgorithmPoi(DeviceAlgorithm deviceAlgorithm)
        {
			Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenTemplatePOIFilterCommand = new RelayCommand(a => OpenTemplatePOIFilter());
            OpenTemplatePoiReviseCommand = new RelayCommand(a => OpenTemplatePoiRevise());
            OpenTemplatePoiOutputCommand = new RelayCommand(a => OpenTemplatePoiOutput());
            OpenPoiFileCommand = new RelayCommand(a => OpenPoiFile());
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplateSelectedIndex;
        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplatePoi(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        public int TemplatePOIFilterSelectedIndex { get => _TemplatePOIFilterSelectedIndex; set { _TemplatePOIFilterSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplatePOIFilterSelectedIndex;

        public void OpenTemplatePOIFilter()
        {
            new TemplateEditorWindow(new TemplatePoiFilterParam(), TemplatePOIFilterSelectedIndex - 1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public int TemplatePoiReviseSelectedIndex { get => _TemplatePoiReviseSelectedIndex; set { _TemplatePoiReviseSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplatePoiReviseSelectedIndex;

        public POIStorageModel POIStorageModel { get => _POIStorageModel; set { _POIStorageModel = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsUSeFile)); } }
        private POIStorageModel _POIStorageModel = POIStorageModel.Db;

        public bool IsUSeFile => POIStorageModel == POIStorageModel.File;

        public bool IsSubPixel { get => _IsSubPixel; set { _IsSubPixel = value; OnPropertyChanged(); } }
        private bool _IsSubPixel;
        public bool IsCCTWave { get => _IsCCTWave; set { _IsCCTWave = value; OnPropertyChanged(); } }
        private bool _IsCCTWave = true;

        public string POIPointFileName { get => _POIPointFileName; set { _POIPointFileName = value; OnPropertyChanged(); } }
        private string _POIPointFileName;

        public void OpenPoiFile()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.tiff|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                POIPointFileName = openFileDialog.FileName;
            }
        }


        public void OpenTemplatePoiRevise()
        {
            new TemplateEditorWindow(new TemplatePoiReviseParam(), TemplatePoiReviseSelectedIndex - 1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public int TemplatePoiOutputSelectedIndex { get => _TemplatePoiOutputSelectedIndex; set { _TemplatePoiOutputSelectedIndex = value; OnPropertyChanged(); } }
        private int _TemplatePoiOutputSelectedIndex;

        public void OpenTemplatePoiOutput()
        {
            new TemplateEditorWindow(new TemplatePoiOutputParam(), TemplatePoiOutputSelectedIndex - 1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayPoi(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }

        public MsgRecord SendCommand(string deviceCode, string deviceType, string fileName, PoiParam poiParam, PoiFilterParam filter, PoiReviseParam revise, PoiOutputParam output, string sn)
        {
            sn = string.IsNullOrWhiteSpace(sn) ? DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff") : sn;

            if (DService.HistoryFilePath.TryGetValue(fileName, out string fullpath))
                fileName = fullpath;
            FileExtType fileExtType = FileExtType.CIE;
            if (Path.GetExtension(fileName).Contains("cvraw"))
            {
                fileExtType = FileExtType.Raw;
            }
            else if (Path.GetExtension(fileName).Contains("cvcie"))
            {
                fileExtType = FileExtType.CIE;
            }
            else if (Path.GetExtension(fileName).Contains("tif"))
            {
                fileExtType = FileExtType.Tif;
            }
            else
            {
                fileExtType = FileExtType.Src;
            }

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType},{ "DeviceCode", deviceCode }, { "DeviceType", deviceType } };

            Params.Add("TemplateParam", new CVTemplateParam() { ID = poiParam.Id, Name = poiParam.Name });
            if (filter.Id != -1)
                Params.Add("FilterTemplate", new CVTemplateParam() { ID = filter.Id, Name = filter.Name });
            if (revise.Id != -1)
                Params.Add("ReviseTemplate", new CVTemplateParam() { ID = revise.Id, Name = revise.Name });
            if (output.Id != -1)
                Params.Add("OutputTemplate", new CVTemplateParam() { ID = output.Id, Name = output.Name });

            if (POIStorageModel == POIStorageModel.File)
            {
                Params.Add("POIStorageType", POIStorageModel);
                Params.Add("POIPointFileName", POIPointFileName);
            }

            Params.Add("IsSubPixel", IsSubPixel);
            Params.Add("IsCCTWave", IsCCTWave);

            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_POI_GetData,
                SerialNumber = sn,
                Params = Params
            };
            return DService.PublishAsyncClient(msg);
        }
    }
}
