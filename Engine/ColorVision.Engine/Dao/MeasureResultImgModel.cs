using ColorVision.Database;
using SqlSugar;
using log4net;
using System;

#pragma warning disable CA1822

namespace ColorVision.Engine
{
    [@SugarTable("t_scgd_measure_result_img")]
    public class MeasureResultImgModel : EntityBase, IInitTables
    {
        [SugarColumn(ColumnName = "batch_id", IsNullable = true)]
        public int BatchId { get; set; }

        [SugarColumn(ColumnName = "z_index", IsNullable = true)]
        public int? ZIndex { get; set; }

        [SugarColumn(ColumnName = "nd_port", ColumnDataType = "tinyint", Length = 2, IsNullable = true, ColumnDescription = "ND滤轮")]
        public int? NDPort { get; set; }

        [SugarColumn(ColumnName = "params", ColumnDataType = "json", IsNullable = true, ColumnDescription = "参数")]
        public string? Params { get; set; }

        [SugarColumn(ColumnName = "smu_data_id", IsNullable = true, ColumnDescription = "SMU result ID")]
        public int? SmuDataId { get; set; }

        [SugarColumn(ColumnName = "i_result", IsNullable = true, ColumnDescription = "源表电流")]
        public float? IResult { get; set; }

        [SugarColumn(ColumnName = "v_result", IsNullable = true, ColumnDescription = "源表电压")]
        public float? VResult { get; set; }

        [SugarColumn(ColumnName = "raw_file", IsNullable = true)]
        public string? RawFile { get; set; }

        [SugarColumn(ColumnName = "file_url", Length = 2048, IsNullable = true, ColumnDescription = "文件URL地址")]
        public string? FileUrl { get; set; }

        [SugarColumn(ColumnName = "file_type", ColumnDataType = "tinyint", Length = 4, IsNullable = true, DefaultValue = "0", ColumnDescription = "文件类型，0:原始文件;1:CIE文件")]
        public sbyte? FileType { get; set; }

        [SugarColumn(ColumnName = "file_data", ColumnDataType = "json", IsNullable = true)]
        public string? ImgFrameInfo { get; set; }

        [SugarColumn(ColumnName = "result_code", IsNullable = true)]
        public int ResultCode { get; set; }

        [SugarColumn(ColumnName = "result", IsNullable = true)]
        public string? Result { get; set; }


        [SugarColumn(ColumnName = "total_time", IsNullable = true, ColumnDescription = "总用时")]
        public int TotalTime { get; set; }


        [SugarColumn(ColumnName ="device_code",IsNullable =true)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = false, DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "创建日期")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }



    public class MeasureImgResultDao : BaseTableDao<MeasureResultImgModel>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MeasureImgResultDao));

        public static MeasureImgResultDao Instance { get;} = new MeasureImgResultDao();

        public int GetLatestId(string? deviceCode)
        {
            if (!MySqlControl.GetInstance().IsConnect) return -1;

            try
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var query = db.Queryable<MeasureResultImgModel>();
                if (!string.IsNullOrWhiteSpace(deviceCode))
                {
                    query = query.Where(x => x.DeviceCode == deviceCode);
                }

                return query.OrderBy(x => x.Id, OrderByType.Desc).First()?.Id ?? 0;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return -1;
            }
        }

        public MeasureResultImgModel? GetLatestAfterId(string? deviceCode, int id)
        {
            if (id < 0 || !MySqlControl.GetInstance().IsConnect) return null;

            try
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var query = db.Queryable<MeasureResultImgModel>().Where(x => x.Id > id);
                if (!string.IsNullOrWhiteSpace(deviceCode))
                {
                    query = query.Where(x => x.DeviceCode == deviceCode);
                }

                return query.OrderBy(x => x.Id, OrderByType.Desc).First();
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return null;
            }
        }
    }
}
