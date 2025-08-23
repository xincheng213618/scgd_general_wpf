using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Dao
{
    public class SysDeviceModel : PKModel
    {
        [SugarColumn(ColumnName ="name")]
        public string? Name { get; set; }
        [SugarColumn(ColumnName ="code")]
        public string? Code { get; set; }
        [SugarColumn(ColumnName ="type_code")]
        public string? TypeCode { get; set; }
        [SugarColumn(ColumnName ="type")]
        public int Type { get; set; }
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }
        [SugarColumn(ColumnName ="pcode")]
        public string? PCode { get; set; }
        [SugarColumn(ColumnName ="txt_value")]
        public string? Value { get; set; }
        [SugarColumn(ColumnName ="create_date")]
        public DateTime CreateDate { get; set; }
        [SugarColumn(ColumnName ="tenant_id")]
        public int TenantId { get; set; }
    }



    public class VSysDeviceDao : BaseViewDao<SysDeviceModel>
    {
        public static VSysDeviceDao Instance { get; set; } = new VSysDeviceDao();

        public VSysDeviceDao() : base("v_scgd_sys_resource_valid_devices", "t_scgd_sys_resource", "id", false)
        {
        }
    }
}
