#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.MVVM;
using ColorVision.Services.Device.Algorithm.Dao;
using ColorVision.Sorts;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.ObjectModel;

namespace ColorVision.Services.Device.Algorithm.Views
{
    public delegate void CurSelectionChanged(AlgorithmResult data);

    public class AlgorithmResult : ViewModelBase, ISortID, ISortBatch, ISortCreateTime, ISortFilePath
    {
        public ObservableCollection<PoiResultData> PoiData { get; set; }
        public ObservableCollection<FOVResultData> FOVData { get; set; }
        public ObservableCollection<MTFResultData> MTFData { get; set; }
        public ObservableCollection<SFRResultData> SFRData { get; set; }
        public ObservableCollection<GhostResultData> GhostData { get; set; }
        public ObservableCollection<DistortionResultData> DistortionData { get; set; }
        public ObservableCollection<LedResultData> LedResultDatas { get; set; }


        public AlgorithmResult(AlgResultMasterModel item) : this(item.Id, item.BatchCode, item.ImgFile, item.TName, item.CreateDate, item.ImgFileType, item.ResultCode, item.Result, item.TotalTime)
        {
        }

        public AlgorithmResult(int id, string serialNumber, string imgFileName, string pOITemplateName, DateTime? recvTime, AlgorithmResultType resultType, int? resultCode, string resultDesc, long totalTime = 0)
        {
            _Id = id;
            Batch = serialNumber;
            FilePath = imgFileName;
            _POITemplateName = pOITemplateName;
            CreateTime = recvTime;
            _ResultType = resultType;
            _resultCode = (int)resultCode;
            _totalTime = totalTime;
            _resultDesc = resultDesc;
            PoiData = new ObservableCollection<PoiResultData>();
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

        public string ResultTypeDis
        {
            get
            {
                string result = "";
                switch (_ResultType)
                {
                    case AlgorithmResultType.POI_XY_UV:
                        result = "色度";
                        break;
                    case AlgorithmResultType.POI_Y:
                        result = "亮度";
                        break;
                    case AlgorithmResultType.POI:
                        result = "关注点";
                        break;
                    case AlgorithmResultType.Distortion:
                        result = "畸变";
                        break;
                    case AlgorithmResultType.Ghost:
                        result = "鬼影";
                        break;
                    default:
                        result = _ResultType.ToString();
                        break;
                }
                return result;
            }
        }
        public AlgorithmResultType ResultType
        {
            get { return _ResultType; }
            set { _ResultType = value; }
        }
        public string Result
        {
            get
            {
                return ResultCode == 0 ? "成功" : "失败";
            }
        }
        public string TotalTime
        {
            get
            {
                return string.Format("{0}", TimeSpan.FromMilliseconds(_totalTime).ToString().TrimEnd('0'));
            }
        }
        public int ResultCode { get { return _resultCode; } set { _resultCode = value; NotifyPropertyChanged(); } }
        public string ResultDesc { get { return _resultDesc; } set { _resultDesc = value; NotifyPropertyChanged(); } }


    }
}
