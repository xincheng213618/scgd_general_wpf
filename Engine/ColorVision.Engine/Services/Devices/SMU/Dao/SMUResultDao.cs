using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.SMU.Dao
{

    [SugarTable("t_scgd_measure_result_smu")]
    public class SMUResultModel : PKModel, IInitTables
    {

        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }

        [SugarColumn(ColumnName ="batch_id")]
        public string? Bid { get; set; }

        [SugarColumn(ColumnName ="is_source_v")]
        public bool IsSourceV { get; set; }

        [SugarColumn(ColumnName ="src_value")]
        public float SrcValue { get; set; }

        [SugarColumn(ColumnName ="limit_value")]
        public float LimitValue { get; set; }

        [SugarColumn(ColumnName ="v_result")]
        public float VResult { get; set; }

        [SugarColumn(ColumnName ="i_result")]
        public float IResult { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; }
    }

    public class SMUResultDao : BaseTableDao<SMUResultModel>
    {

        public static SMUResultDao Instance { get; set; } = new SMUResultDao();

        public List<SMUResultModel> selectBySN(string sn)
        {
            List<SMUResultModel> list = new();
            DataTable d_info = GetTableAllBySN(sn);
            foreach (var item in d_info.AsEnumerable())
            {
                SMUResultModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        private DataTable GetTableAllBySN(string bid)
        {
            string sql = $"select * from {TableName} where batch_id='{bid}'";
            DataTable d_info = GetData(sql);
            return d_info;
        }
    }
}
