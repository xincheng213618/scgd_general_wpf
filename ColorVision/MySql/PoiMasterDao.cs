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
    public class PoiMasterModel : IBaseModel
    {
        public PoiMasterModel() : this("")
        {
        }

        public PoiMasterModel(string name)
        {
            Id = -1;
            Name = name;
            Type = 0;
            Width = 400;
            Height = 300;
            LeftTopX = 0;
            LeftTopY = 0;
            RightTopX = 400;
            RightTopY = 0;
            RightBottomX = 400;
            RightBottomY = 300;
            LeftBottomX = 0;
            LeftBottomY = 300;
            IsDynamics = false;
            CreateDate = DateTime.Now;
            IsEnable = true;
            IsDelete = false;
        }

        public PoiMasterModel(PoiParam poiParam)
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
            RightBottomX = poiParam.DatumAreaPoints.X3X;
            RightBottomY = poiParam.DatumAreaPoints.X3Y;
            LeftBottomX = poiParam.DatumAreaPoints.X4X;
            LeftBottomY = poiParam.DatumAreaPoints.X4Y;
            IsDynamics = false;
            CfgJson = poiParam.CfgJson;
            CreateDate = DateTime.Now;
            IsEnable = true;
            IsDelete = false;
        }



        public int Id { get; set; }
        public string Name { get; set; }

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
        public string? CfgJson { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public bool? IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
        public string? Remark { get; set; }

        public int GetPK()
        {
            return Id;
        }

        public void SetPK(int id)
        {
           Id = id;
        }
    }



    internal class PoiMasterDao : BaseServiceMaster<PoiMasterModel>
    {
        public PoiMasterDao() : base("t_scgd_cfg_poi_master","id")
        {
            
        }
        public override DataTable GetDataTable(string? tableName =null)
        {
            DataTable dataTable = base.GetDataTable();
            return CreateColumns(dataTable);
        }

        public override PoiMasterModel GetModel(DataRow item)
        {
            PoiMasterModel model = new PoiMasterModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string>("name"),
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
                CfgJson = item.Field<string?>("cfg_json"),
                CreateDate = item.Field<DateTime?>("create_date"),
                IsEnable = item.Field<bool?>("is_enable"),
                IsDelete = item.Field<bool?>("is_delete"),
                Remark = item.Field<string?>("remark"),
            };
            return model;
        }

        public override DataRow Model2Row(PoiMasterModel item, DataRow row)
        {
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
                if (item.CfgJson != null) row["cfg_json"] = item.CfgJson;
                row["dynamics"] = item.IsDynamics;
                row["create_date"] = item.CreateDate;
                //row["is_enable"] = item.IsEnable;
                //row["is_delete"] = item.IsDelete;
                if (item.Remark != null) row["remark"] = item.Remark;
            }
            return row;
        }

        public override DataTable CreateColumns(DataTable dataTable)
        {
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
            dataTable.Columns.Add("cfg_json", typeof(string));
            dataTable.Columns.Add("create_date", typeof(DateTime));
            dataTable.Columns.Add("is_enable", typeof(bool));
            dataTable.Columns.Add("is_delete", typeof(bool));
            dataTable.Columns.Add("remark", typeof(string));
            return dataTable;
        }
    }
}
