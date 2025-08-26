using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    public class PoiResult : ViewModelBase, IViewResult
    {



        private int _Id;
        private string _SerialNumber;
        private string _ImgFileName;
        private string _POITemplateName;
        private string _RecvTime;
        private ViewResultAlgType _ResultType;
        private ObservableCollection<PoiResultData> _PoiData;

        public int Id { get { return _Id; } set { _Id = value; OnPropertyChanged(); } }
        public string SerialNumber { get { return _SerialNumber; } set { _SerialNumber = value; OnPropertyChanged(); } }
        public string ImgFileName { get { return _ImgFileName; } set { _ImgFileName = value; OnPropertyChanged(); } }
        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; OnPropertyChanged(); } }
        public string RecvTime { get { return _RecvTime; } set { _RecvTime = value; OnPropertyChanged(); } }

        public string ResultTypeDis
        {
            get
            {
                string result = "";
                switch (_ResultType)
                {
                    case ViewResultAlgType.POI_XYZ:
                        result = "色度";
                        break;
                    case ViewResultAlgType.POI_Y:
                        result = "亮度";
                        break;
                    default:
                        break;
                }
                return result;
            }
        }
        public ViewResultAlgType ResultType
        {
            get { return _ResultType; }
            set { _ResultType = value; }
        }
        public ObservableCollection<PoiResultData> PoiData { get { return _PoiData; } set { _PoiData = value; OnPropertyChanged(); } }

        public PoiResult()
        {
            _PoiData = new ObservableCollection<PoiResultData>();
        }

        public PoiResult(int id, string serialNumber, string imgFileName, string pOITemplateName, string recvTime, ViewResultAlgType resultType) : this()
        {
            _Id = id;
            _SerialNumber = serialNumber;
            _ImgFileName = imgFileName;
            _POITemplateName = pOITemplateName;
            _RecvTime = recvTime;
            _ResultType = resultType;
        }
    }
}
