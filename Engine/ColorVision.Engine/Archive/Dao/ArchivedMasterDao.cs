#pragma warning disable CS8603
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using log4net;
using MySql.Data.MySqlClient;
using NPOI.SS.Formula.Functions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Archive.Dao
{


    [SugarTable("t_scgd_archived_master")]
    public class ArchivedMasterModel : PKModel
    {
        [SugarColumn(ColumnName = "code"),DisplayName("Code")]
        public string Code { get; set; }
        [SugarColumn(ColumnName ="name"),DisplayName("名称")]
        public string Name { get; set; }
        [SugarColumn(ColumnName ="data"),DisplayName("Data")]
        public string Data { get; set; }
        [SugarColumn(ColumnName ="remark")]
        public string Remark { get; set; }
        [SugarColumn(ColumnName ="tenant_id")]
        public int? TenantId { get; set; }
        [SugarColumn(ColumnName ="create_date"),DisplayName("创建日期")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        [SugarColumn(ColumnName ="arch_date"),DisplayName("归档日期")]
        public DateTime? ArchDate { get; set; } = DateTime.Now;

    }
    public class ArchivedMasterDao : BaseTableDao<ArchivedMasterModel>
    {

        public static ArchivedMasterDao Instance { get; set; } = new ArchivedMasterDao();
        public override MySqlConnection CreateConnection()
        {
            MySqlConfig MySqlConfig = GlobleCfgdDao.Instance.GetArchMySqlConfig();
            if (MySqlConfig != null)
            {
                string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={3};SSL Mode =None;Pooling=true";
                var conn = new MySqlConnection(connStr);
                conn.Open();
                return conn;
            }
            return null;
        }


        public List<ArchivedMasterModel> ConditionalQuery(string batchCode)
        {
            return ConditionalQuery(new Dictionary<string, object>() { { "code", batchCode } });
        }
    }
}
