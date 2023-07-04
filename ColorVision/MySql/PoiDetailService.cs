using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{

    public class PoiDetailModel
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public int? Pid { get; set; }
        public int? Type { get; set; }
        public int? PixX { get; set; }
        public int? PixY { get; set; }
        public int? PixWidth { get; set; }
        public int? PixHeight { get; set; }
        public DateTime? CreateDate { get; set; }
        public bool? IsEnable { get; set; }
        public bool? IsDelete { get; set; }
        public string? Remark { get; set; }
    }


    public class PoiDetailService : BaseServiceMaster<PoiDetailModel>
    {
        public PoiDetailService() : base("t_scgd_cfg_poi_detail")
        {
        }


        public override DataTable GetDataTable(string? tableName = null)
        {
            DataTable dataTable = base.GetDataTable();
            dataTable.Columns.Add("id", typeof(int));
            dataTable.Columns.Add("name", typeof(string));
            dataTable.Columns.Add("pt_type", typeof(sbyte));
            dataTable.Columns.Add("pid", typeof(int));
            dataTable.Columns.Add("pix_width", typeof(int));
            dataTable.Columns.Add("pix_height", typeof(int));
            dataTable.Columns.Add("pix_x", typeof(int));
            dataTable.Columns.Add("pix_y", typeof(int));
            dataTable.Columns.Add("create_date", typeof(DateTime));
            dataTable.Columns.Add("is_enable", typeof(bool));
            dataTable.Columns.Add("is_delete", typeof(bool));
            dataTable.Columns.Add("remark", typeof(string));
            return dataTable;
        }


        public override PoiDetailModel GetModel(DataRow item)
        {
            PoiDetailModel model = new PoiDetailModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
                Type = item.Field<sbyte>("pt_type"),
                Pid = item.Field<int>("pid"),
                PixWidth = item.Field<int>("pix_width"),
                PixHeight = item.Field<int>("pix_height"),
                PixX = item.Field<int>("pix_x"),
                PixY = item.Field<int>("pix_y"),
                CreateDate = item.Field<DateTime>("create_date"),
                IsEnable = item.Field<bool>("is_enable"),
                IsDelete = item.Field<bool>("is_delete"),
                Remark = item.Field<string>("remark"),
            };
            return model;
        }

        public override DataRow GetRow(PoiDetailModel item, DataTable d_info)
        {
            DataRow row = base.GetRow(item, d_info);
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                if (item.Name != null) row["name"] = item.Name;
                if (item.Type >= 0) row["pt_type"] = item.Type;
                if (item.Pid > 0) row["pid"] = item.Pid;
                if (item.PixWidth > 0) row["pix_width"] = item.PixWidth;
                if (item.PixHeight > 0) row["pix_height"] = item.PixHeight;
                if (item.PixX >= 0) row["pix_x"] = item.PixX;
                if (item.PixY >= 0) row["pix_y"] = item.PixY;
                row["create_date"] = item.CreateDate;
                row["is_enable"] = item.IsEnable;
                row["is_delete"] = item.IsDelete;
                if (item.Remark != null) row["remark"] = item.Remark;
            }
            return row;
        }
    }
}
