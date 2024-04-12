#pragma warning disable CS8604,CS8629
using ColorVision.Common.Sorts;
using ColorVision.Common.MVVM;
using ColorVision.Services.Dao;
using MQTTMessageLib.Camera;
using System;
using System.IO;
using ColorVision.Services.Export;
using ColorVision.Media;
using ColorVision.Net;
using System.Windows;
using ColorVision.Common.Utilities;

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
            FileUrl = measureImgResultModel.FileUrl ?? string.Empty;
            FileType = (CameraFileType)(measureImgResultModel.FileType ?? 0);
            ReqParams = measureImgResultModel.ReqParams ?? string.Empty;
            ImgFrameInfo = measureImgResultModel.ImgFrameInfo ?? string.Empty;
            CreateTime = measureImgResultModel.CreateDate;
            ResultCode = measureImgResultModel.ResultCode;
            ResultMsg = measureImgResultModel.ResultMsg;
            ResultDesc = measureImgResultModel.ResultMsg ?? string.Empty;
            _totalTime = measureImgResultModel.TotalTime;

            ExportCVCIECommand = new RelayCommand(a => Export(), a => File.Exists(FileUrl) );
            OpenCVCIECommand = new RelayCommand(a => Open(), a => File.Exists(FileUrl));
        }


        public void Export()
        {
            ExportCVCIE exportCVCIE = new ExportCVCIE(FileUrl);
            exportCVCIE.Show();
        }
        public void Open()
        {
            ImageView imageView = new ImageView();
            CVFileUtil.ReadCVRaw(FileUrl, out CVCIEFile fileInfo);
            Window window = new Window() { Title = "快速预览", Owner = Application.Current.GetActiveWindow() ,WindowStartupLocation = WindowStartupLocation.CenterOwner};
            window.Content = imageView;
            imageView.OpenImage(fileInfo);
            window.Show();
        }

        public RelayCommand ExportCVCIECommand { get; set; }
        public RelayCommand OpenCVCIECommand { get; set; }


public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;
        public int IdShow { get; set; }

        public string? Batch { get => _Batch; set { _Batch = value; NotifyPropertyChanged(); } }
        private string? _Batch;
        public string? FileUrl { get => _FileUrl; set { _FileUrl = value; NotifyPropertyChanged(); } }
        private string? _FileUrl;
        public string? FilePath { get => _FilePath; set { _FilePath = value; NotifyPropertyChanged(); } }
        private string? _FilePath;

        public CameraFileType FileType { get => _FileType; set { _FileType = value; NotifyPropertyChanged(); } }
        private CameraFileType _FileType;

        public string ReqParams { get => _Params; set { _Params = value; NotifyPropertyChanged(); } }
        private string _Params;

        public string ImgFrameInfo { get => _ImgFrameInfo; set { _ImgFrameInfo = value; NotifyPropertyChanged(); } }
        private string _ImgFrameInfo;

        public DateTime? CreateTime { get => _RecvTime;  set { _RecvTime = value; NotifyPropertyChanged(); } }
        private DateTime? _RecvTime;

        public string? ResultMsg { get => _ResultMsg; set { _ResultMsg = value; NotifyPropertyChanged(); } }
        private string? _ResultMsg;
        public int ResultCode { get => _resultCode; set { _resultCode = value; NotifyPropertyChanged(); } }
        private int _resultCode;

        public string TotalTime => string.Format("{0}", TimeSpan.FromMilliseconds(_totalTime).ToString(@"mm\:ss\:fff"));
        private long _totalTime;

        private string _resultDesc;



        public string ResultDesc { get => _resultDesc; set { _resultDesc = value; NotifyPropertyChanged(); } }
    }


}
