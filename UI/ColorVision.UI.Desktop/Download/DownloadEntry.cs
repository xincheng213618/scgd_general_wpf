using SqlSugar;
using System;

namespace ColorVision.UI.Desktop.Download
{
    [SugarIndex("Index_CreateTime", nameof(CreateTime), OrderByType.Desc)]
    [SugarIndex("Index_Status", nameof(Status), OrderByType.Asc)]
    public class DownloadEntry
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(IsNullable = false, ColumnDataType = "text")]
        public string Url { get; set; } = string.Empty;

        [SugarColumn(IsNullable = false, ColumnDataType = "text")]
        public string FileName { get; set; } = string.Empty;

        [SugarColumn(IsNullable = false, ColumnDataType = "text")]
        public string SavePath { get; set; } = string.Empty;

        /// <summary>
        /// 0=Waiting, 1=Downloading, 2=Completed, 3=Failed, 4=Paused, 5=FileDeleted
        /// </summary>
        public int Status { get; set; }

        public long TotalBytes { get; set; }

        public long DownloadedBytes { get; set; }

        public DateTime CreateTime { get; set; } = DateTime.Now;

        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public DateTime? CompleteTime { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string? ErrorMessage { get; set; }
    }

    public enum DownloadStatus
    {
        Waiting = 0,
        Downloading = 1,
        Completed = 2,
        Failed = 3,
        Paused = 4,
        FileDeleted = 5
    }
}
