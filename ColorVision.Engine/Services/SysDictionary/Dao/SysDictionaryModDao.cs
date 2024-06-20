using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Engine.Services.SysDictionary
{
    public class SysDictionaryModModel : ViewModelBase,IPKModel
    {
        public int Id { get; set; }
        public int? PId { get => _PId; set { _PId = value; NotifyPropertyChanged(); } }
        private int? _PId;

        public short ModType { get => _ModType; set { _ModType = value; NotifyPropertyChanged(); } }
        private short _ModType;

        public string? Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string? _Code;

        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        public int TenantId { get => _TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; NotifyPropertyChanged(); } }
        private DateTime _CreateDate;
    }

    public class SysDictionaryModDao : BaseTableDao<SysDictionaryModModel>
    {
        public static SysDictionaryModDao Instance { get; set; } = new SysDictionaryModDao();

        public SysDictionaryModDao() : base("t_scgd_sys_dictionary_mod_master", "id")
        {
        }
        public override DataTable CreateColumns(DataTable dataTable)
        {
            dataTable.Columns.Add("id", typeof(int));
            dataTable.Columns.Add("code", typeof(string));
            dataTable.Columns.Add("name", typeof(string));
            dataTable.Columns.Add("tenant_id", typeof(int));
            dataTable.Columns.Add("pid", typeof(int?));
            dataTable.Columns.Add("mod_type", typeof(short));
            dataTable.Columns.Add("create_date", typeof(DateTime));
            return dataTable;        
        }

        public override DataRow Model2Row(SysDictionaryModModel item, DataRow row)
        {
            if (item.Id > 0) row["id"] = item.Id;
            row["code"] = DataTableExtension.IsDBNull(item.Code);
            row["name"] = DataTableExtension.IsDBNull(item.Name);
            row["tenant_id"] = DataTableExtension.IsDBNull(item.TenantId);
            row["pid"] = DataTableExtension.IsDBNull(item.PId);
            row["mod_type"] = DataTableExtension.IsDBNull(item.ModType);
            row["create_date"] = DataTableExtension.IsDBNull(item.CreateDate);
            return row;
            
        }

        public override SysDictionaryModModel GetModelFromDataRow(DataRow item)
        {
            SysDictionaryModModel model = new()
            {
                Id = item.Field<int>("id"),
                Code = item.Field<string>("code"),
                Name = item.Field<string>("name"),
                TenantId = item.Field<int>("tenant_id"),
                PId = item.Field<int?>("pid"),
                ModType = item.Field<short>("mod_type"),
                CreateDate = item.Field<DateTime>("create_date")
            };

            return model;
        }

        public SysDictionaryModModel GetByCode(string? code, int tenantId)
        {
            if (string.IsNullOrEmpty(code))
                return new SysDictionaryModModel();
            string sql = $"select * from {TableName} where is_delete=0 and code=@code and tenant_id=@tenantId";
            Dictionary<string, object> param = new()
            {
                { "code", code },
                { "tenantId", tenantId }
            };
            DataTable d_info = GetData(sql, param);
            return d_info.Rows.Count == 1 ? GetModelFromDataRow(d_info.Rows[0]) : new SysDictionaryModModel();
        }
    }
}
