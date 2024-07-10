#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.UI.Sorts;
using ColorVision.Engine.Services.Devices.Algorithm.Dao;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Distortion;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.FOV;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Compliance;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck;
using MQTTMessageLib.Algorithm;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public delegate void CurSelectionChanged(AlgorithmResult data);

    public class AlgorithmResult : ViewModelBase, ISortID, ISortBatch, ISortCreateTime, ISortFilePath
    {
        public ObservableCollection<PoiResultData> PoiResultDatas { get; set; }
        public ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas { get; set; }
        public ObservableCollection<PoiResultCIEYData> PoiResultCIEYDatas { get; set; }
        public ObservableCollection<ViewResultFOV> FOVData { get; set; }
        public ObservableCollection<ViewResultMTF> MTFData { get; set; }
        public ObservableCollection<ViewResultBuildPoi> BuildPoiResultData { get; set; }
        public ObservableCollection<ViewResultSFR> SFRData { get; set; }
        public ObservableCollection<ViewResultGhost> GhostData { get; set; }
        public ObservableCollection<ViewResultDistortion> DistortionData { get; set; }
        public ObservableCollection<ViewResultLedCheck> LedResultDatas { get; set; }
        public List<ComplianceYModel> ComplianceYDatas { get; set; }
        public List<ComplianceXYZModel> ComplianceXYZDatas { get; set; }


        public AlgorithmResult(AlgResultMasterModel item)
        {
            Id = item.Id;
            Batch = item.BatchCode;
            FilePath = item.ImgFile;
            _POITemplateName = item.TName;
            CreateTime = item.CreateDate;
            _ResultType = item.ImgFileType;
            _resultCode = (int)item.ResultCode;
            _totalTime = item.TotalTime;
            _resultDesc = item.Result;
        }

        private AlgorithmResultType _ResultType;
        private int _resultCode;
        private long _totalTime;
        private string _resultDesc;

        public int Id { get { return _Id; } set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string? Batch { get { return _Batch; } set { _Batch = value; NotifyPropertyChanged(); } }
        private string? _Batch;

        public string? FilePath { get { return _FilePath; } set { _FilePath = value; NotifyPropertyChanged(); } }
        private string? _FilePath;

        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; NotifyPropertyChanged(); } }
        private string _POITemplateName;

        public DateTime? CreateTime { get { return _CreateTime; } set { _CreateTime = value; NotifyPropertyChanged(); } }
        private DateTime? _CreateTime;

        public string ResultTypeDis => _ResultType switch
        {
            AlgorithmResultType.POI_XYZ => "色度",
            AlgorithmResultType.POI_Y => "亮度",
            AlgorithmResultType.POI => "关注点",
            AlgorithmResultType.Distortion => "畸变",
            AlgorithmResultType.Ghost => "鬼影",
            _ => _ResultType.ToString(),
        };

        public AlgorithmResultType ResultType
        {
            get { return _ResultType; }
            set { _ResultType = value; }
        }

        public string ResultDesc { get { return _resultDesc; } set { _resultDesc = value; NotifyPropertyChanged(); } }
        public string TotalTime => string.Format("{0}", TimeSpan.FromMilliseconds(_totalTime).ToString().TrimEnd('0'));
        public int ResultCode { get { return _resultCode; } set { _resultCode = value; NotifyPropertyChanged(); } }
        public string Result => ResultCode == 0 ? "成功" : "失败";


    }
}
