﻿using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
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
        private AlgorithmResultType _ResultType;
        private ObservableCollection<PoiResultData> _PoiData;

        public int Id { get { return _Id; } set { _Id = value; NotifyPropertyChanged(); } }
        public string SerialNumber { get { return _SerialNumber; } set { _SerialNumber = value; NotifyPropertyChanged(); } }
        public string ImgFileName { get { return _ImgFileName; } set { _ImgFileName = value; NotifyPropertyChanged(); } }
        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; NotifyPropertyChanged(); } }
        public string RecvTime { get { return _RecvTime; } set { _RecvTime = value; NotifyPropertyChanged(); } }

        public string ResultTypeDis
        {
            get
            {
                string result = "";
                switch (_ResultType)
                {
                    case AlgorithmResultType.POI_XYZ:
                        result = "色度";
                        break;
                    case AlgorithmResultType.POI_Y:
                        result = "亮度";
                        break;
                    default:
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
        public ObservableCollection<PoiResultData> PoiData { get { return _PoiData; } set { _PoiData = value; NotifyPropertyChanged(); } }

        public PoiResult()
        {
            _PoiData = new ObservableCollection<PoiResultData>();
        }

        public PoiResult(int id, string serialNumber, string imgFileName, string pOITemplateName, string recvTime, AlgorithmResultType resultType) : this()
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
