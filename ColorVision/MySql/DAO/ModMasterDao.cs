using NPOI.SS.Formula.Functions;
using OpenCvSharp.Flann;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ColorVision.MySql.DAO
{
    public class ModMasterModel : PKModel
    {
        public ModMasterModel() : this("","",0){ }
        public ModMasterModel(string pcode, string text, int tenantId)
        {
            Pcode = pcode;
            Name = text;
            TenantId = tenantId;
            CreateDate = DateTime.Now;
        }

        public string? Name { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public bool? IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
        public string? Remark { get; set; }
        public int TenantId { get; set; }
        public int Pid { get; set; }
        public string? Pcode { get; set; }
    }
    public class ModMasterDao : BaseDaoMaster<ModMasterModel>
    {
        private string _code;
        public ModMasterDao(string code) : base("v_scgd_mod_master", "t_scgd_mod_param_master", "id", true)
        {
            _code = code;
        }

        public ModMasterDao() : base("v_scgd_mod_master", "t_scgd_mod_param_master", "id", true)
        {
            _code = string.Empty;
        }

        public override DataTable GetTableAllByTenantId(int tenantId)
        {
            string sql;
            if (string.IsNullOrEmpty(ViewName)) sql = $"select * from {TableName} where is_delete=0 and tenant_id={tenantId} and pcode='{_code}'";
            else sql = $"select * from {ViewName} where is_delete=0 and tenant_id={tenantId} and pcode='{_code}'";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public string GetPCode() { return _code; }

        public override ModMasterModel GetModel(DataRow item)
        {
            ModMasterModel model = new ModMasterModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string?>("name"),
                CreateDate = item.Field<DateTime?>("create_date"),
                IsEnable = item.Field<bool?>("is_enable"),
                IsDelete = item.Field<bool?>("is_delete"),
                Remark = item.Field<string?>("remark"),
                Pcode = item.Field<string>("pcode"),
            };

            return model;
        }

        public override DataRow Model2Row(ModMasterModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                if (item.Name != null) row["name"] = item.Name;
                row["create_date"] = item.CreateDate;
                //row["is_enable"] = item.IsEnable;
                //row["is_delete"] = item.IsDelete;
                if (item.Remark != null) row["remark"] = item.Remark;
                row["tenant_id"] = item.TenantId;
                row["mm_id"] = item.Pid;
            }
            return row;
        }
    }
}
