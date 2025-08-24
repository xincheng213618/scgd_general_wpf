﻿using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ProjectARVR.Config
{
    public class SummaryInfo : ViewModelBase
    {
        public bool IsShowSummary { get => _IsShowSummary; set { _IsShowSummary = value; OnPropertyChanged(); } }
        private bool _IsShowSummary;

        public double Width { get => _Width; set { _Width = value; OnPropertyChanged(); } }
        private double _Width = 300;

        /// <summary>
        /// 线别
        /// </summary>
        public string LineNumber { get => _LineNumber; set { _LineNumber = value; OnPropertyChanged(); } }
        private string _LineNumber;

        /// <summary>
        /// 工号
        /// </summary>
        public string WorkerNumber { get => _WorkerNumber; set { _WorkerNumber = value; OnPropertyChanged(); } }
        private string _WorkerNumber;

        /// <summary>
        /// 目标生产
        /// </summary>
        public int TargetProduction { get => _TargetProduction; set { _TargetProduction = value; OnPropertyChanged(); } }
        private int _TargetProduction;

        /// <summary>
        /// 已生产
        /// </summary>
        public int ActualProduction { get => _ActualProduction; set { _ActualProduction = value; OnPropertyChanged(); } }
        private int _ActualProduction;
        /// <summary>
        /// 良品数量
        /// </summary>
        public int GoodProductCount { get => _GoodProductCount; set { _GoodProductCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(GoodProductRate)); } }
        private int _GoodProductCount;

        /// <summary>
        /// 不良品数量
        /// </summary>
        public int DefectiveProductCount { get => _DefectiveProductCount; set { _DefectiveProductCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(DefectiveProductRate)); } }
        private int _DefectiveProductCount;

        /// <summary>
        /// 良品率
        /// </summary>
        [JsonIgnore]
        public double GoodProductRate { get => ActualProduction > 0 ? (double)GoodProductCount / (double)ActualProduction : 0; }

        /// <summary>
        /// 不良率
        /// </summary>
        [JsonIgnore]
        public double DefectiveProductRate { get => ActualProduction > 0 ? (double)DefectiveProductCount / (double)ActualProduction : 0; }



    }
}
