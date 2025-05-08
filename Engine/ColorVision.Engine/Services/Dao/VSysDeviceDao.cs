using ColorVision.Engine.MySql.ORM;
using System;
using System.Data;

namespace ColorVision.Engine.Services.Dao
{
    public class SysDeviceModel : PKModel
    {
        [Column("name")]
        public string? Name { get; set; }
        [Column("code")]
        public string? Code { get; set; }
        [Column("type_code")]
        public string? TypeCode { get; set; }
        [Column("type")]
        public int Type { get; set; }
        [Column("pid")]
        public int? Pid { get; set; }
        [Column("pcode")]
        public string? PCode { get; set; }
        [Column("txt_value")]
        public string? Value { get; set; }
        [Column("create_date")]
        public DateTime CreateDate { get; set; }
        [Column("tenant_id")]
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
