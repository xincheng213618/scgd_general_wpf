using ColorVision.Common.MVVM;
using ColorVision.Engine.Interfaces;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public struct PointFloat
    {
        public float X { get; set; }

        public float Y { get; set; }

        public PointFloat(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public class AlgorithmBuildPoi : DisplayAlgorithmBase
    {
        public string Name { get; set; } = "关注点布点";
        public int Order { get; set; } = 2;

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm DService { get => Device.DService; }

        public RelayCommand OpenTemplateCommand { get; set; }

        public RelayCommand OpenCADFileCommand { get; set; }

        public AlgorithmBuildPoi(DeviceAlgorithm deviceAlgorithm)
        {
            Name = "关注点布点";
            Order = 2;


            Device = deviceAlgorithm;
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenCADFileCommand = new RelayCommand(a => OpenCADFile());
        }

        public void OpenCADFile()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.tiff|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CADPosFileName = openFileDialog.FileName;
            }
        }

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;

        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateBuildPoi(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public override UserControl GetUserControl()
        {
            UserControl ??= new DisplayBuildPoi(this);
            return UserControl;
        }
        public UserControl UserControl { get; set; }


        public POIStorageModel POIStorageModel { get => _POIStorageModel; set { _POIStorageModel = value; NotifyPropertyChanged(); } }
        private POIStorageModel _POIStorageModel = POIStorageModel.Db;


        public POIBuildType POIBuildType { get => _POIBuildType; set { _POIBuildType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsPOIBuildCommon)); } }
        private POIBuildType _POIBuildType = POIBuildType.Common;

        public bool IsPOIBuildCommon => POIBuildType == POIBuildType.Common;

        public PointFloat Point1 { get => _Point1; set { _Point1 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point1;
        public PointFloat Point2 { get => _Point2; set { _Point2 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point2;
        public PointFloat Point3 { get => _Point3; set { _Point3 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point3;
        public PointFloat Point4 { get => _Point4; set { _Point4 = value; NotifyPropertyChanged(); } }
        private PointFloat _Point4;

        public string CADPosFileName { get => _CADPosFileName; set { _CADPosFileName = value; NotifyPropertyChanged(); } }
        private string _CADPosFileName = string.Empty;

        public MsgRecord SendCommand(ParamBuildPoi buildPOIParam, POILayoutTypes POILayoutReq, Dictionary<string, object> @params, string deviceCode, string deviceType, string fileName, FileExtType fileExtType, string serialNumber)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;

            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = buildPOIParam.Id, Name = buildPOIParam.Name });
            Params.Add("POILayoutReq", POILayoutReq.ToString());
            Params.Add("POIStorageType", POIStorageModel);
            Params.Add("BuildType", POIBuildType);
            if (POIBuildType == POIBuildType.CADMapping)
            {
                List<PointInt> pointInts = new List<PointInt>();
                pointInts.Add(new PointInt() { X = (int)Point1.X, Y = (int)Point1.Y });
                pointInts.Add(new PointInt() { X = (int)Point2.X, Y = (int)Point2.Y });
                pointInts.Add(new PointInt() { X = (int)Point3.X, Y = (int)Point3.Y });
                pointInts.Add(new PointInt() { X = (int)Point4.X, Y = (int)Point4.Y });

                Params.Add("LayoutPolygon", pointInts); 

                PointFloat[] ROI = new PointFloat[] { Point1, Point2, Point3, Point4 };
                Params.Add("CADMappingParam", new Dictionary<string, Object>() { { "CAD_MasterId", -1 },{ "ROI" , ROI },{ "CAD_PosFileName" , CADPosFileName } });
            }

            foreach (var param in @params)
            {
                Params.Add(param.Key, param.Value);
            }
            MsgSend msg = new()
            {
                EventName = MQTTAlgorithmEventEnum.Event_Build_POI,
                SerialNumber = sn,
                Params = Params
            };

            return DService.PublishAsyncClient(msg);
        }
    }
}
