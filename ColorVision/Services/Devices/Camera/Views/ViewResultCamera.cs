﻿#pragma warning disable CS8604,CS8629
using ColorVision.Common.Sorts;
using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using MQTTMessageLib.Camera;
using System;

namespace ColorVision.Services.Devices.Camera.Views
{
    public delegate void ImgCurSelectionChanged(ViewResultCamera data);


    public enum ImageLayer
    {
        Src,
        R,
        G,
        B,
        X,
        Y,
        Z
    }

    public class ViewResultCamera : ViewModelBase,ISortID,ISortBatch, ISortCreateTime, ISortFilePath
    {
        public ViewResultCamera(MeasureImgResultModel measureImgResultModel)
        {
            Id = measureImgResultModel.Id;
            Batch = measureImgResultModel.BatchCode ?? string.Empty;
            FilePath = measureImgResultModel.RawFile ?? string.Empty;
            FileType = (CameraFileType)(measureImgResultModel.FileType ?? 0);
            ReqParams = measureImgResultModel.ReqParams ?? string.Empty;
            ImgFrameInfo = measureImgResultModel.ImgFrameInfo ?? string.Empty;
            CreateTime = measureImgResultModel.CreateDate;
            ResultCode = measureImgResultModel.ResultCode;
            ResultMsg = measureImgResultModel.ResultMsg;
            ResultDesc = measureImgResultModel.ResultMsg ?? string.Empty;
            _totalTime = measureImgResultModel.TotalTime;
        }

        public int Id { get { return _ID; } set { _ID = value; NotifyPropertyChanged(); } }
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

        public string? ResultMsg { get => _ResultMsg; set { _ResultMsg = value; NotifyPropertyChanged(); } }
        private string? _ResultMsg;
        public int ResultCode { get { return _resultCode; } set { _resultCode = value; NotifyPropertyChanged(); } }
        private int _resultCode;

        public string TotalTime => string.Format("{0}", TimeSpan.FromMilliseconds(_totalTime).ToString(@"mm\:ss\:fff"));
        private long _totalTime;

        private string _resultDesc;



        public string ResultDesc { get { return _resultDesc; } set { _resultDesc = value; NotifyPropertyChanged(); } }
    }


}
