using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.SMU.Dao
{

    [SugarTable("t_scgd_measure_result_smu")]
    public class SMUResultModel : PKModel
    {

        [Column("pid")]
        public int? Pid { get; set; }

        [Column("batch_id")]
        public string? Bid { get; set; }

        [Column("is_source_v")]
        public bool IsSourceV { get; set; }

        [Column("src_value")]
        public float SrcValue { get; set; }

        [Column("limit_value")]
        public float LimitValue { get; set; }

        [Column("v_result")]
        public float VResult { get; set; }

        [Column("i_result")]
        public float IResult { get; set; }

        [Column("create_date")]
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
