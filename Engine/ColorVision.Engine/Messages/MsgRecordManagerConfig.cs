using ColorVision.Common.MVVM;
using ColorVision.UI;
using SqlSugar;
using System;
using System.ComponentModel;

namespace ColorVision.Engine.Messages
{
    public class MsgRecordManagerConfig : ViewModelBase, IConfig
    {
        public string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";

        public string SqliteDbPath { get => DirectoryPath + "MsgRecords.db"; }

        [DisplayName("QueryCount"), Category("View")]
        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count = 50;

        [DisplayName("SortByType"), Category("View")]
        public OrderByType OrderByType { get => _OrderByType; set { _OrderByType = value; OnPropertyChanged(); } }
        private OrderByType _OrderByType = OrderByType.Desc;


    }
}
