using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;

namespace ColorVision.Engine.Templates.Flow
{
    public class FlowNodeRecordConfig : ViewModelBase, IConfig
    {
        public string DirectoryPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ColorVision\\Config\\";

        public string SqliteDbPath { get => DirectoryPath + "FlowNodeRecords.db"; }
    }
}
