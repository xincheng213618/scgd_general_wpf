#pragma warning disable CS8604,CS8629
using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.UIExport.SolutionExports.Export;
using ColorVision.Net;
using ColorVision.UI.Sorts;
using MQTTMessageLib.Camera;
using System;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Views
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
            ExportCVCIE exportCVCIE = new(FileUrl);
            exportCVCIE.Show();
        }
        public void Open()
        {
            if (File.Exists(FileUrl))
            {
                ImageView imageView = new();

                CVFileUtil.ReadCVRaw(FileUrl, out CVCIEFile fileInfo);
                Window window = new() { Title = Properties.Resources.QuickPreview, Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                window.Content = imageView;
                imageView.OpenImage(new NetFileUtil().OpenLocalCVFile(FileUrl));

                window.Show();
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() => {
                    imageView.ToolBarTop.ClearImage();
                }));
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到文件", "ColorVision");
            }

        }

        public RelayCommand ExportCVCIECommand { get; set; }
        public RelayCommand OpenCVCIECommand { get; set; }
        
        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

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
