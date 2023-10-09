using System;
using System.Data;

namespace ColorVision.MySql.DAO
{
    public class LicenseModel : PKModel
    {
        public string? CustomerName { get; set; }
        public string? MacSn { get; set; }
        public string? Model { get; set; }
        public string? Value { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public bool IsEnable { get; set; } = true;
        public bool IsDelete { get; set; }
        public string? Remark { get; set; }


    }
    public class LicenseDao : BaseDaoMaster<LicenseModel>
    {
        public LicenseDao() : base(string.Empty, "t_scgd_license", "id", true)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("customer_name", typeof(string));
            dInfo.Columns.Add("mac_sn", typeof(string));
            dInfo.Columns.Add("model", typeof(string));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("create_date", typeof(DateTime));
            dInfo.Columns.Add("is_enable", typeof(bool));
            dInfo.Columns.Add("is_delete", typeof(bool));
            dInfo.Columns.Add("remark", typeof(string));
            return dInfo;
        }


        public override LicenseModel GetModel(DataRow item)
        {
            LicenseModel model = new LicenseModel
            {
                Id = item.Field<int>("id"),
                CustomerName = item.Field<string>("customer_name"),
                MacSn = item.Field<string>("mac_sn"),
                Model = item.Field<string>("model"),
                Value = item.Field<string>("value"),
                CreateDate = item.Field<DateTime?>("create_date"),
                IsEnable = item.Field<bool>("is_enable"),
                IsDelete = item.Field<bool>("is_delete"),
                Remark = item.Field<string>("remark")
            };
            return model;
        }

        public override DataRow Model2Row(LicenseModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                row["customer_name"] = item.CustomerName;
                row["mac_sn"] = item.MacSn;
                row["model"] = item.Model;
                row["value"] = item.Value;
                row["create_date"] = item.CreateDate;
                row["is_enable"] = item.IsEnable;
                row["is_delete"] = item.IsDelete;
                row["remark"] = item.Remark;
            }
            return row;
        }



    }
}
