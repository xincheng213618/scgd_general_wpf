using ColorVision.Template;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{
    public class PoiMasterModel
    {
        public PoiMasterModel()
        {

        }

        public PoiMasterModel(PoiParam poiParam )
        {
            Id = poiParam.ID;
            Name = poiParam.PoiName;
            Type = poiParam.Type;
            Width = poiParam.Width;
            Height = poiParam.Height;
            LeftTopX = poiParam.DatumAreaPoints.X1X;
            LeftTopY = poiParam.DatumAreaPoints.X1Y;
            RightTopX = poiParam.DatumAreaPoints.X2X;
            RightTopY = poiParam.DatumAreaPoints.X2Y;
            LeftBottomX = poiParam.DatumAreaPoints.X3X;
            LeftBottomY = poiParam.DatumAreaPoints.X3Y;
            RightBottomX = poiParam.DatumAreaPoints.X4X;
            RightBottomY = poiParam.DatumAreaPoints.X4Y;
        }



        public int Id { get; set; }
        public string? Name { get; set; }

        public int Type { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int? LeftTopX { get; set; }
        public int? LeftTopY { get; set; }
        public int? RightTopX { get; set; }
        public int? RightTopY { get; set; }
        public int? RightBottomX { get; set; }
        public int? RightBottomY { get; set; }
        public int? LeftBottomX { get; set; }
        public int? LeftBottomY { get; set; }
        public bool? IsDynamics { get; set; } = false;
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public bool? IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
        public string? Remark { get; set; }
    }



    internal class PoiMasterDao : BaseServiceMaster<PoiMasterModel>
    {
        public PoiMasterDao() : base("t_scgd_cfg_poi_master")
        {
            
        }
        public override DataTable GetDataTable(string? tableName =null)
        {
            DataTable dataTable = base.GetDataTable();
            dataTable.Columns.Add("id", typeof(int));
            dataTable.Columns.Add("name", typeof(string));
            dataTable.Columns.Add("type", typeof(sbyte));
            dataTable.Columns.Add("width", typeof(int));
            dataTable.Columns.Add("height", typeof(int));
            dataTable.Columns.Add("left_top_x", typeof(int));
            dataTable.Columns.Add("left_top_y", typeof(int));
            dataTable.Columns.Add("right_top_x", typeof(int));
            dataTable.Columns.Add("right_top_y", typeof(int));
            dataTable.Columns.Add("right_bottom_x", typeof(int));
            dataTable.Columns.Add("right_bottom_y", typeof(int));
            dataTable.Columns.Add("left_bottom_x", typeof(int));
            dataTable.Columns.Add("left_bottom_y", typeof(int));
            dataTable.Columns.Add("dynamics", typeof(bool));
            dataTable.Columns.Add("create_date", typeof(DateTime));
            dataTable.Columns.Add("is_enable", typeof(bool));
            dataTable.Columns.Add("is_delete", typeof(bool));
            dataTable.Columns.Add("remark", typeof(string));
            return dataTable;
        }

        public override PoiMasterModel GetModel(DataRow item)
        {
            PoiMasterModel model = new PoiMasterModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string?>("name"),
                Type = item.Field<sbyte>("type"),
                Width = item.Field<int>("width"),
                Height = item.Field<int>("height"),
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
            DataRow row = base.GetRow(item, d_info);
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                if (item.Name != null) row["name"] = item.Name;
                if (item.Type >= 0) row["type"] = item.Type;
                if (item.Width > 0) row["width"] = item.Width;
                if (item.Height > 0) row["height"] = item.Height;
                if (item.LeftTopX >= 0) row["left_top_x"] = item.LeftTopX;
                if (item.LeftTopY >= 0) row["left_top_y"] = item.LeftTopY;
                if (item.RightTopX >= 0) row["right_top_x"] = item.RightTopX;
                if (item.RightTopY >= 0) row["right_top_y"] = item.RightTopY;
                if (item.RightBottomX >= 0) row["right_bottom_x"] = item.RightBottomX;
                if (item.RightBottomY >= 0) row["right_bottom_y"] = item.RightBottomY;
                if (item.LeftBottomX >= 0) row["left_bottom_x"] = item.LeftBottomX;
                if (item.LeftBottomY >= 0) row["left_bottom_y"] = item.LeftBottomY;
                row["dynamics"] = item.IsDynamics;
                row["create_date"] = item.CreateDate;
                row["is_enable"] = item.IsEnable;
                row["is_delete"] = item.IsDelete;
                if (item.Remark != null) row["remark"] = item.Remark;
            }

            return row;
        }

        public override DataTable CreateColumns(DataTable d_info)
        {
            d_info.Columns.Add("id", typeof(int));
            d_info.Columns.Add("name", typeof(string));
            d_info.Columns.Add("type", typeof(sbyte));
            d_info.Columns.Add("width", typeof(int));
            d_info.Columns.Add("height", typeof(int));
            d_info.Columns.Add("left_top_x", typeof(int));
            d_info.Columns.Add("left_top_y", typeof(int));
            d_info.Columns.Add("right_top_x", typeof(int));
            d_info.Columns.Add("right_top_y", typeof(int));
            d_info.Columns.Add("right_bottom_x", typeof(int));
            d_info.Columns.Add("right_bottom_y", typeof(int));
            d_info.Columns.Add("left_bottom_x", typeof(int));
            d_info.Columns.Add("left_bottom_y", typeof(int));
            d_info.Columns.Add("dynamics", typeof(bool));
            d_info.Columns.Add("create_date", typeof(DateTime));
            d_info.Columns.Add("is_enable", typeof(bool));
            d_info.Columns.Add("is_delete", typeof(bool));
            d_info.Columns.Add("remark", typeof(string));
            return d_info;
        }
    }
}
