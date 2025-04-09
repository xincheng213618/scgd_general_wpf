using ColorVision.Common.MVVM;
using ColorVision.Engine.Interfaces;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIRevise;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class AlgorithmPoi : ViewModelBase, IDisplayAlgorithm
    {
        public string Name { get; set; } = "POI";
        public int Order { get; set; } = 1;


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

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;
        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplatePoi(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }


        public int TemplatePOIFilterSelectedIndex { get => _TemplatePOIFilterSelectedIndex; set { _TemplatePOIFilterSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePOIFilterSelectedIndex;

        public void OpenTemplatePOIFilter()
        {
            new TemplateEditorWindow(new TemplatePoiFilterParam(), TemplatePOIFilterSelectedIndex - 1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public int TemplatePoiReviseSelectedIndex { get => _TemplatePoiReviseSelectedIndex; set { _TemplatePoiReviseSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePoiReviseSelectedIndex;

        public POIStorageModel POIStorageModel { get => _POIStorageModel; set { _POIStorageModel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsUSeFile)); } }
        private POIStorageModel _POIStorageModel = POIStorageModel.Db;

        public bool IsUSeFile => POIStorageModel == POIStorageModel.File;


        public string POIPointFileName { get => _POIPointFileName; set { _POIPointFileName = value; NotifyPropertyChanged(); } }
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

        public int TemplatePoiOutputSelectedIndex { get => _TemplatePoiOutputSelectedIndex; set { _TemplatePoiOutputSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplatePoiOutputSelectedIndex;

        public void OpenTemplatePoiOutput()
        {
            new TemplateEditorWindow(new TemplatePoiOutputParam(), TemplatePoiOutputSelectedIndex - 1) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public UserControl GetUserControl()
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

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };

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
