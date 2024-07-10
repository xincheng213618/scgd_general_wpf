#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.MVVM;
using ColorVision.UI.Sorts;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public class AlgorithmResult : ViewModelBase, ISortID, ISortBatch, ISortCreateTime, ISortFilePath
    {
        public ObservableCollection<IViewResult> ViewResults { get; set; }

        public AlgorithmResult(AlgResultMasterModel item)
        {
            Id = item.Id;
            Batch = item.BatchCode;
            FilePath = item.ImgFile;
            POITemplateName = item.TName;
            CreateTime = item.CreateDate;
            ResultType = item.ImgFileType;
            ResultCode = item.ResultCode;
            TotalTime = item.TotalTime;
            ResultDesc = item.Result;
        }

        public int Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string? Batch { get { return _Batch; } set { _Batch = value; NotifyPropertyChanged(); } }
        private string? _Batch;

        public string? FilePath { get { return _FilePath; } set { _FilePath = value; NotifyPropertyChanged(); } }
        private string? _FilePath;

        public string POITemplateName { get { return _POITemplateName; } set { _POITemplateName = value; NotifyPropertyChanged(); } }
        private string _POITemplateName;

        public DateTime? CreateTime { get { return _CreateTime; } set { _CreateTime = value; NotifyPropertyChanged(); } }
        private DateTime? _CreateTime;


        public AlgorithmResultType ResultType {get=> _ResultType; set { _ResultType = value; NotifyPropertyChanged(); } }
        private AlgorithmResultType _ResultType;

        public string ResultDesc { get { return _ResultDesc; } set { _ResultDesc = value; NotifyPropertyChanged(); } }
        private string _ResultDesc;

        public long TotalTime { get => _TotalTime; set { _TotalTime = value; NotifyPropertyChanged(); } }
        private long _TotalTime;

        public int? ResultCode { get { return _ResultCode; } set { _ResultCode = value; NotifyPropertyChanged(); } }
        private int? _ResultCode;

        public string Result => ResultCode == 0 ? "成功" : "失败";


    }
}
