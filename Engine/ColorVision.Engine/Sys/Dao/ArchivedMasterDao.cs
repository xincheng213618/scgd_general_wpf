using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using log4net;
using MySql.Data.MySqlClient;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace ColorVision.Engine.DataHistory.Dao
{



    public class ArchivedMasterModel : PKModel
    {
        [Column("code"),DisplayName("Code")]
        public string Code { get; set; }
        [Column("name"),DisplayName("名称")]
        public string Name { get; set; }
        [Column("data"),DisplayName("Data")]
        public string Data { get; set; }
        [Column("remark")]
        public string Remark { get; set; }
        [Column("tenant_id")]
        public int? TenantId { get; set; }
        [Column("create_date"),DisplayName("创建日期")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("arch_date"),DisplayName("归档日期")]
        public DateTime? ArchDate { get; set; } = DateTime.Now;

    }
    public class ArchivedMasterDao : BaseTableDao<ArchivedMasterModel>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ArchivedMasterDao));

        public static ArchivedMasterDao Instance { get; set; } = new ArchivedMasterDao();

        public ArchivedMasterDao() : base("t_scgd_archived_master", "code")
        {
            MySqlConfig MySqlConfig = GlobleCfgdDao.Instance.GetArchMySqlConfig();
            if (MySqlConfig != null)
            {
                try
                {
                    string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={3};SSL Mode =None;Pooling=true";
                    MySqlConnection = new MySqlConnection(connStr = connStr);
                    MySqlConnection.Open();
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    MySqlControl = MySqlControl.GetInstance();
                    MySqlControl.MySqlConnectChanged += (s, e) => MySqlConnection = MySqlControl.MySqlConnection;
                    MySqlConnection = MySqlControl.MySqlConnection;
                }
            }


        }

        public List<ArchivedMasterModel> ConditionalQuery(string batchCode)
        {
            return ConditionalQuery(new Dictionary<string, object>() { { "code", batchCode } });
        }
    }
}
