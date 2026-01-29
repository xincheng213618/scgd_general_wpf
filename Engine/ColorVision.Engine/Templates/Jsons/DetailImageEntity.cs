using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Templates.Jsons
{
    [SugarTable("t_scgd_algorithm_result_detail_image")]
    public class DetailImageEntity: EntityBase, IInitTables, IViewResult
    {
        /// <summary>
        /// PID
        /// </summary>
        [SugarColumn(ColumnName = "pid", IsNullable = true)]
        public int? PId { get; set; }

        /// <summary>
        /// 结果图像文件
        /// </summary>
        [SugarColumn(ColumnName = "file_name", Length = 2048, IsNullable = true, ColumnDescription = "结果图像文件")]
        public string FileName { get; set; }

        /// <summary>
        /// 排序索引
        /// </summary>
        [SugarColumn(ColumnName = "order_index", IsNullable = true)]
        public int? OrderIndex { get; set; }

        /// <summary>
        /// 文件信息
        /// </summary>
        [SugarColumn(ColumnName = "file_info", ColumnDataType = "json", IsNullable = true)]
        public string FileInfo { get; set; }
    }


}
