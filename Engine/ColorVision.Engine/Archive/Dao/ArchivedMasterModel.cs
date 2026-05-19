using SqlSugar;
using System;
using System.ComponentModel;

namespace ColorVision.Engine.Archive.Dao
{


    [SugarTable("t_scgd_archived_master")]
    public class ArchivedMasterModel 
    {
        [SugarColumn(ColumnName = "code", IsPrimaryKey = true), ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "Code")]
        public string Code { get; set; }
        [SugarColumn(ColumnName ="name"), ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "Name")]
        public string Name { get; set; }
        [SugarColumn(ColumnName ="data"),DisplayName("Data")]
        public string Data { get; set; }
        [SugarColumn(ColumnName ="remark")]
        public string Remark { get; set; }
        [SugarColumn(ColumnName ="tenant_id")]
        public int? TenantId { get; set; }
        [SugarColumn(ColumnName ="create_date"), ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "CreationDate")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        [SugarColumn(ColumnName ="arch_date"), ColorVision.Engine.Utilities.LocalizedDisplayName(typeof(ColorVision.Engine.Properties.Resources), "ArchiveDate")]
        public DateTime? ArchDate { get; set; } = DateTime.Now;
    }
}
