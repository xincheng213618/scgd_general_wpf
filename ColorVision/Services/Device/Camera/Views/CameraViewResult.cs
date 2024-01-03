#pragma warning disable CS8604,CS8629
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using MQTTMessageLib.Camera;
using System;

namespace ColorVision.Services.Device.Camera.Views
{
    public class CameraViewResult : ViewModelBase
    {
        public CameraViewResult(MeasureImgResultModel measureImgResultModel)
        {
            Id = measureImgResultModel.Id;
            SerialNumber = measureImgResultModel.BatchCode ?? string.Empty;
            ImgFileName = measureImgResultModel.RawFile ?? string.Empty;
            FileType = (CameraFileType)measureImgResultModel.FileType;
            ReqParams = measureImgResultModel.ReqParams ?? string.Empty;
            ImgFrameInfo = measureImgResultModel.ImgFrameInfo ?? string.Empty;
            RecvTime = measureImgResultModel.CreateDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            ResultCode = measureImgResultModel.ResultCode;
            ResultDesc = measureImgResultModel.ResultDesc ?? string.Empty;
            _totalTime = measureImgResultModel.TotalTime;
        }

        public int Id { get { return _Id; } set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string SerialNumber { get { return _SerialNumber; } set { _SerialNumber = value; NotifyPropertyChanged(); } }
        private string _SerialNumber;

        public string ImgFileName { get { return _ImgFileName; } set { _ImgFileName = value; NotifyPropertyChanged(); } }
        private string _ImgFileName;

        public CameraFileType FileType { get { return _FileType; } set { _FileType = value; NotifyPropertyChanged(); } }
        private CameraFileType _FileType;

        public string ReqParams { get { return _Params; } set { _Params = value; NotifyPropertyChanged(); } }
        private string _Params;

        public string ImgFrameInfo { get { return _ImgFrameInfo; } set { _ImgFrameInfo = value; NotifyPropertyChanged(); } }
        private string _ImgFrameInfo;

        public string RecvTime { get { return _RecvTime; } set { _RecvTime = value; NotifyPropertyChanged(); } }
        private string _RecvTime;

        public string Result
        {
            get
            {
                return ResultCode == 0 ? "成功" : "失败";
            }
        }
        private int _resultCode;

        public string TotalTime
        {
            get
            {
                return string.Format("{0}", TimeSpan.FromMilliseconds(_totalTime).ToString().TrimEnd('0'));
            }
        }
        private string _resultDesc;

        public int ResultCode { get { return _resultCode; } set { _resultCode = value; NotifyPropertyChanged(); } }
        public string ResultDesc { get { return _resultDesc; } set { _resultDesc = value; NotifyPropertyChanged(); } }
        private long _totalTime;
    }


}
