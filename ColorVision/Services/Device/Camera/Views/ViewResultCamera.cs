#pragma warning disable CS8604,CS8629
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Sorts;
using MQTTMessageLib.Camera;
using System;

namespace ColorVision.Services.Device.Camera.Views
{
    public delegate void ImgCurSelectionChanged(ViewResultCamera data);

    public class ViewResultCamera : ViewModelBase,ISortID,ISortBatch, ISortCreateTime, ISortFilePath
    {
        public ViewResultCamera(MeasureImgResultModel measureImgResultModel)
        {
            ID = measureImgResultModel.Id;
            Batch = measureImgResultModel.BatchCode ?? string.Empty;
            FilePath = measureImgResultModel.RawFile ?? string.Empty;
            FileType = (CameraFileType)measureImgResultModel.FileType;
            ReqParams = measureImgResultModel.ReqParams ?? string.Empty;
            ImgFrameInfo = measureImgResultModel.ImgFrameInfo ?? string.Empty;
            CreateTime = measureImgResultModel.CreateDate;
            ResultCode = measureImgResultModel.ResultCode;
            ResultDesc = measureImgResultModel.ResultDesc ?? string.Empty;
            _totalTime = measureImgResultModel.TotalTime;
        }

        public int ID { get { return _ID; } set { _ID = value; NotifyPropertyChanged(); } }
        private int _ID;

        public string? Batch { get { return _Batch; } set { _Batch = value; NotifyPropertyChanged(); } }
        private string? _Batch;

        public string? FilePath { get { return _FilePath; } set { _FilePath = value; NotifyPropertyChanged(); } }
        private string? _FilePath;

        public CameraFileType FileType { get { return _FileType; } set { _FileType = value; NotifyPropertyChanged(); } }
        private CameraFileType _FileType;

        public string ReqParams { get { return _Params; } set { _Params = value; NotifyPropertyChanged(); } }
        private string _Params;

        public string ImgFrameInfo { get { return _ImgFrameInfo; } set { _ImgFrameInfo = value; NotifyPropertyChanged(); } }
        private string _ImgFrameInfo;

        public DateTime? CreateTime { get { return _RecvTime; } set { _RecvTime = value; NotifyPropertyChanged(); } }
        private DateTime? _RecvTime;

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
        private long _totalTime;

        private string _resultDesc;

        public int ResultCode { get { return _resultCode; } set { _resultCode = value; NotifyPropertyChanged(); } }
        public string ResultDesc { get { return _resultDesc; } set { _resultDesc = value; NotifyPropertyChanged(); } }
    }


}
