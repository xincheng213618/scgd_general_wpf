#pragma warning disable CA1720,CS8601

using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Engine.Services.ShowPage.Dao
{
    public class ArchivedDetailModel : PKModel
    {
        [Column("guid")]
        public string Guid { get; set; }
        [Column("p_guid")]
        public string PGuid { get; set; }
        [Column("detail_type")]
        public string DetailType { get; set; }
        [Column("z_index")]
        public int? ZIndex { get; set; }
        [Column("output_value")]
        public string OutputValue { get; set; }
        [Column("device_code")]
        public string DeviceCode { get; set; }
        [Column("device_cfg")]
        public string DeviceCfg { get; set; }

        [Column("input_cfg")]
        public string InputCfg { get; set; }

    }
    public class ArchivedDetailDao : BaseTableDao<ArchivedDetailModel>
    {
        public static ArchivedDetailDao Instance { get; set; } = new ArchivedDetailDao();

        public ArchivedDetailDao() : base("t_scgd_archived_detail", "guid")
        {

        }

        public List<ArchivedDetailModel> ConditionalQuery(string batchCode)
        {
            return ConditionalQuery(new Dictionary<string, object>() { { "p_guid", batchCode } });
        }
    }
}
