#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.ObjectModel;

namespace ColorVision.Services.Algorithm.Views
{
    public delegate void CurSelectionChanged(AlgorithmResult data);

    public class AlgorithmResult : ViewModelBase
    {
        private int _Id;
        private string _SerialNumber;
        private string _ImgFileName;
        private string _POITemplateName;
        private string? _RecvTime;
        private AlgorithmResultType _ResultType;
        private int _resultCode;
        private long _totalTime;
        private string _resultDesc;
        private ObservableCollection<PoiResultData> _PoiData;
        private ObservableCollection<FOVResultData> _FOVData;
        private ObservableCollection<MTFResultData> _MTFData;
        private ObservableCollection<SFRResultData> _SFRData;
        private ObservableCollection<GhostResultData> _GhostData;
        private ObservableCollection<DistortionResultData> _DistortionData;
        public int Id { get { return _Id; } set { _Id = value; NotifyPropertyChanged(); } }
        public string SerialNumber { get { return _SerialNumber; } set { _SerialNumber = value; NotifyPropertyChanged(); } }
        public string ImgFileName { get { return _ImgFileName; } set { _ImgFileName = value; NotifyPropertyChanged(); } }
        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; NotifyPropertyChanged(); } }
        public string? RecvTime { get { return _RecvTime; } set { _RecvTime = value; NotifyPropertyChanged(); } }

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
        public ObservableCollection<PoiResultData> PoiData { get { return _PoiData; } set { _PoiData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<FOVResultData> FOVData { get { return _FOVData; } set { _FOVData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<MTFResultData> MTFData { get { return _MTFData; } set { _MTFData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<SFRResultData> SFRData { get { return _SFRData; } set { _SFRData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<GhostResultData> GhostData { get { return _GhostData; } set { _GhostData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<DistortionResultData> DistortionData { get { return _DistortionData; } set { _DistortionData = value; NotifyPropertyChanged(); } }
        public ObservableCollection<LedResultData> LedResultDatas { get; set; }

        public AlgorithmResult()
        {
            this._PoiData = new ObservableCollection<PoiResultData>();
            this._FOVData = new ObservableCollection<FOVResultData>();
            this._MTFData = new ObservableCollection<MTFResultData>();
            this._SFRData = new ObservableCollection<SFRResultData>();
            this._GhostData = new ObservableCollection<GhostResultData>();
            this._DistortionData = new ObservableCollection<DistortionResultData>();
            LedResultDatas = new ObservableCollection<LedResultData>();
        }

        public AlgorithmResult(AlgResultMasterModel algResultMasterModel)
        {
            _Id = algResultMasterModel.Id;
            _SerialNumber = algResultMasterModel.BatchCode;
            _ImgFileName = algResultMasterModel.ImgFile;
            _POITemplateName = algResultMasterModel.TName;
            _RecvTime = algResultMasterModel.CreateDate.ToString();
            _ResultType = algResultMasterModel.ImgFileType;
            _resultCode = (int)algResultMasterModel.ResultCode;
            _totalTime = algResultMasterModel.TotalTime;
            _resultDesc = algResultMasterModel.Result;
        }

        public AlgorithmResult(int id, string serialNumber, string imgFileName, string pOITemplateName, string recvTime, AlgorithmResultType resultType, int? resultCode, string resultDesc, long totalTime = 0) : this()
        {
            _Id = id;
            _SerialNumber = serialNumber;
            _ImgFileName = imgFileName;
            _POITemplateName = pOITemplateName;
            _RecvTime = recvTime;
            _ResultType = resultType;
            _resultCode = (int)resultCode;
            _totalTime = totalTime;
            _resultDesc = resultDesc;
        }
    }
}
