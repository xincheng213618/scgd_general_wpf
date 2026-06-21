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
        [SugarColumn(ColumnName ="batch_id")]
        public int BatchId { get; set; }

        [SugarColumn(ColumnName = "z_index", IsNullable = true)]
        public int? ZIndex { get; set; }

        [SugarColumn(ColumnName = "nd_port", ColumnDataType = "tinyint", Length = 2, IsNullable = true, ColumnDescription = "ND滤轮")]
        public int? NDPort { get; set; }

        [SugarColumn(ColumnName = "params" ,ColumnDataType ="json")]
        public string? Params { get; set; }

        [SugarColumn(ColumnName = "smu_data_id", IsNullable = true)]
        public int? SmuDataId { get; set; }

        [SugarColumn(ColumnName = "i_result", IsNullable = true)]
        public double? IResult { get; set; }

        [SugarColumn(ColumnName = "v_result", IsNullable = true)]
        public double? VResult { get; set; }

        [SugarColumn(ColumnName ="raw_file")]
        public string? RawFile { get; set; }

        [SugarColumn(ColumnName ="file_url")]
        public string? FileUrl { get; set; }

        [SugarColumn(ColumnName = "file_type")]
        public sbyte? FileType { get; set; }

        [SugarColumn(ColumnName = "file_data", ColumnDataType = "json")]
        public string? ImgFrameInfo { get; set; }

        [SugarColumn(ColumnName = "result_code")]
        public int ResultCode { get; set; }

        [SugarColumn(ColumnName = "result")]
        public string? Result { get; set; }


        [SugarColumn(ColumnName = "total_time")]
        public int TotalTime { get; set; }


        [SugarColumn(ColumnName ="device_code",IsNullable =true)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "create_date")]
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
