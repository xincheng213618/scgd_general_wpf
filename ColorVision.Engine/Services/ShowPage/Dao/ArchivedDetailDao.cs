#pragma warning disable CA1720,CS8601

using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Engine.Services.ShowPage.Dao
{
    public class ArchivedDetailModel : PKModel
    {
        public string Guid { get; set; }
        public string PGuid { get; set; }
        public string DetailType { get; set; }
        public int? ZIndex { get; set; }
        public string OutputValue { get; set; }


    }
    public class ArchivedDetailDao : BaseTableDao<ArchivedDetailModel>
    {
        public static ArchivedDetailDao Instance { get; set; } = new ArchivedDetailDao();

        public ArchivedDetailDao() : base("t_scgd_archived_detail", "guid")
        {

        }

        public override ArchivedDetailModel GetModelFromDataRow(DataRow item) => new()
        {
            Guid = item.Field<string>("guid"),
            PGuid = item.Field<string>("p_guid"),
            DetailType = item.Field<string>("detail_type"),
            ZIndex = item.Field<int?>("z_index"),
            OutputValue = item.Field<string>("output_value"),
        };

        public List<ArchivedDetailModel> ConditionalQuery(string batchCode)
        {
            return ConditionalQuery(new Dictionary<string, object>() { { "p_guid", batchCode } });
        }
    }
}
