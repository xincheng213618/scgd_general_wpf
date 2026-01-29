using SqlSugar;
using System;

namespace ColorVision.Solution.MultiImageViewer
{
    /// <summary>
    /// 缩略图缓存条目实体
    /// </summary>
    [SugarTable("ThumbnailCache")]
    public class ThumbnailCacheEntry
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// 原始文件完整路径
        /// </summary>
        [SugarColumn(IsNullable = false, Length = 1024)]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 文件最后修改时间，用于判断缓存是否过期
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime FileLastModified { get; set; }

        /// <summary>
        /// 缩略图数据 (PNG格式的二进制数据)
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDataType = "blob")]
        public byte[]? ThumbnailData { get; set; }

        /// <summary>
        /// 缩略图宽度
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int ThumbnailWidth { get; set; }

        /// <summary>
        /// 缩略图高度
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int ThumbnailHeight { get; set; }

        /// <summary>
        /// 原始图像宽度
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int OriginalWidth { get; set; }

        /// <summary>
        /// 原始图像高度
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int OriginalHeight { get; set; }

        /// <summary>
        /// 原始文件大小 (字节)
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public long FileSize { get; set; }

        /// <summary>
        /// 缓存创建时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreateDate { get; set; } = DateTime.Now;
    }
}
