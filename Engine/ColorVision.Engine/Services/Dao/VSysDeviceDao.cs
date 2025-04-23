using ColorVision.Engine.MySql.ORM;
using System;
using System.Data;

namespace ColorVision.Engine.Services.Dao
{
    public class SysDeviceModel : PKModel
    {
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? TypeCode { get; set; }
        public int Type { get; set; }
        public int? Pid { get; set; }
        public string? PCode { get; set; }
        public string? Value { get; set; }
        public DateTime CreateDate { get; set; }
        public int TenantId { get; set; }
    }
    public class VSysDeviceDao : BaseViewDao<SysDeviceModel>
    {
        public static VSysDeviceDao Instance { get; set; } = new VSysDeviceDao();

        public VSysDeviceDao() : base("v_scgd_sys_resource_valid_devices", "t_scgd_sys_resource", "id", false)
        {
        }

        public override SysDeviceModel GetModelFromDataRow(DataRow item)
        {
            SysDeviceModel model = new()
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Code = item.Field<string>("code"),
                Type = item.Field<int>("type"),
                Pid = item.Field<int?>("pid"),
                PCode = item.Field<string>("pcode"),
                TypeCode = item.Field<string>("type_code"),
                Value = item.Field<string>("txt_value"),
                CreateDate = item.Field<DateTime>("create_date"),
                TenantId = item.Field<int>("tenant_id"),
            };
            return model;
        }
    }
}
