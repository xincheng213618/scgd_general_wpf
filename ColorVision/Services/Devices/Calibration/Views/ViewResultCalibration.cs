#pragma warning disable CS8604,CS8629
using ColorVision.Common.Sorts;
using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using MQTTMessageLib.Camera;
using System;

namespace ColorVision.Services.Devices.Calibration.Views
{
    public delegate void ImgCurSelectionChanged1(ViewResultCalibration data);

    public class ViewResultCalibration : ViewModelBase,ISortID,ISortBatch, ISortCreateTime, ISortFilePath
    {
        public ViewResultCalibration(MeasureImgResultModel measureImgResultModel)
        {
            Id = measureImgResultModel.Id;
            Batch = measureImgResultModel.BatchCode ?? string.Empty;
            FilePath = measureImgResultModel.RawFile ?? string.Empty;
            FileType = (CameraFileType)(measureImgResultModel.FileType??0);
            ReqParams = measureImgResultModel.ReqParams ?? string.Empty;
            FileUrl =measureImgResultModel.FileUrl ?? string.Empty;
            ImgFrameInfo = measureImgResultModel.ImgFrameInfo ?? string.Empty;
            CreateTime = measureImgResultModel.CreateDate;
            ResultCode = measureImgResultModel.ResultCode;
            ResultMsg = measureImgResultModel.ResultMsg;
            ResultDesc = measureImgResultModel.ResultMsg ?? string.Empty;
            _totalTime = measureImgResultModel.TotalTime;
        }


        public int Id { get => _Id;  set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string? Batch { get => _Batch;  set { _Batch = value; NotifyPropertyChanged(); } }
        private string? _Batch;

        public string? FileUrl { get => _FileUrl;  set { _FileUrl = value; NotifyPropertyChanged(); } }
        private string? _FileUrl;
        public string? FilePath { get => _FilePath; set { _FilePath = value; NotifyPropertyChanged(); } }
        private string? _FilePath;

        public CameraFileType FileType { get => _FileType; set { _FileType = value; NotifyPropertyChanged(); } }
        private CameraFileType _FileType;

        public string ReqParams { get => _Params; set { _Params = value; NotifyPropertyChanged(); } }
        private string _Params;

        public string ImgFrameInfo { get => _ImgFrameInfo; set { _ImgFrameInfo = value; NotifyPropertyChanged(); } }
        private string _ImgFrameInfo;

        public DateTime? CreateTime { get => _RecvTime; set { _RecvTime = value; NotifyPropertyChanged(); } }
        private DateTime? _RecvTime;

        public string? ResultMsg { get => _ResultMsg; set { _ResultMsg = value; NotifyPropertyChanged(); } }
        private string? _ResultMsg;
        public int ResultCode { get => _resultCode; set { _resultCode = value; NotifyPropertyChanged(); } }
        private int _resultCode;

        public string TotalTime => string.Format("{0}", TimeSpan.FromMilliseconds(_totalTime).ToString().TrimEnd('0'));
        private long _totalTime;


        public string ResultDesc { get => _resultDesc; set { _resultDesc = value; NotifyPropertyChanged(); } }
        private string _resultDesc;

    }


}
