using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{
    internal class PoiMasterModel
    {
        public int? Id { get; set; }
        public string? Name { get; set; }

        public int? Type { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? LeftTopX { get; set; }
        public int? LeftTopY { get; set; }
        public int? RightTopX { get; set; }
        public int? RightTopY { get; set; }
        public int? RightBottomX { get; set; }
        public int? RightBottomY { get; set; }
        public int? LeftBottomX { get; set; }
        public int? LeftBottomY { get; set; }
        public bool? IsDynamics { get; set; }
        public DateTime? CreateDate { get; set; }
        public bool? IsEnable { get; set; }
        public bool? IsDelete { get; set; }
        public string? Remark { get; set; }
    }



    internal class PoiMasterService : BaseServiceMaster<PoiMasterModel>
    {
        public PoiMasterService() : base("t_scgd_cfg_poi_master")
        {
            
        }

        public override PoiMasterModel GetModel(DataRow item)
        {
            PoiMasterModel model = new PoiMasterModel
            {
                Id = item.Field<int?>("id"),
                Name = item.Field<string?>("name"),
                Type = item.Field<sbyte?>("type"),
                Width = item.Field<int?>("width"),
                Height = item.Field<int?>("height"),
                LeftTopX = item.Field<int?>("left_top_x"),
                LeftTopY = item.Field<int?>("left_top_y"),
                RightTopX = item.Field<int?>("right_top_x"),
                RightTopY = item.Field<int?>("right_top_y"),
                RightBottomX = item.Field<int?>("right_bottom_x"),
                RightBottomY = item.Field<int?>("right_bottom_y"),
                LeftBottomX = item.Field<int?>("left_bottom_x"),
                LeftBottomY = item.Field<int?>("left_bottom_y"),
                IsDynamics =item.Field<bool?>("dynamics"),
                CreateDate = item.Field<DateTime?>("create_date"),
                IsEnable = item.Field<bool?>("is_enable"),
                IsDelete = item.Field<bool?>("is_delete"),
                Remark = item.Field<string?>("remark"),
            };
            return model;
        }

        public override DataRow GetRow(PoiMasterModel item, DataTable d_info)
        {
            d_info.Columns.Add();
            DataRow row = base.GetRow(item, d_info);
            if (item != null)
            {
                if (item.Id > 0) row[0] = item.Id;
                if (item.Name != null) row[1] = item.Name;
                if (item.Type >= 0) row[2] = item.Type;
                if (item.Width > 0) row[3] = item.Width;
                if (item.Height > 0) row[4] = item.Height;
                if (item.LeftTopX >= 0) row[5] = item.LeftTopX;
                if (item.LeftTopY >= 0) row[6] = item.LeftTopY;
                if (item.RightTopX >= 0) row[7] = item.RightTopX;
                if (item.RightTopY >= 0) row[8] = item.RightTopY;
                if (item.RightBottomX >= 0) row[9] = item.RightBottomX;
                if (item.RightBottomY >= 0) row[10] = item.RightBottomY;
                if (item.LeftBottomX >= 0) row[11] = item.LeftBottomX;
                if (item.LeftBottomY >= 0) row[12] = item.LeftBottomY;
                row[13] = item.IsDynamics;
                //row["create_date"] = item.CreateDate;
                //row["is_enable"] = item.IsEnable;
                //row["is_delete"] = item.IsDelete;
                if (item.Remark != null) row[14] = item.Remark;
            }
            return row;
        }
    }
}
