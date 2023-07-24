using ColorVision.Template;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class PoiMasterModel : PKModel
    {
        public PoiMasterModel() : this("", 0)
        {
        }

        public PoiMasterModel(string name, int tenantId)
        {
            Id = -1;
            Name = name;
            Type = 0;
            Width = 0;
            Height = 0;
            LeftTopX = 0;
            LeftTopY = 0;
            RightTopX = 0;
            RightTopY = 0;
            RightBottomX = 0;
            RightBottomY = 0;
            LeftBottomX = 0;
            LeftBottomY = 0;
            IsDynamics = false;
            CreateDate = DateTime.Now;
            IsEnable = true;
            IsDelete = false;
            TenantId = tenantId;
        }

        public PoiMasterModel(PoiParam poiParam)
        {
            Id = poiParam.ID;
            Name = poiParam.PoiName;
            Type = poiParam.Type;
            Width = poiParam.Width;
            Height = poiParam.Height;
            LeftTopX = poiParam.DatumArea.X1X;
            LeftTopY = poiParam.DatumArea.X1Y;
            RightTopX = poiParam.DatumArea.X2X;
            RightTopY = poiParam.DatumArea.X2Y;
            RightBottomX = poiParam.DatumArea.X3X;
            RightBottomY = poiParam.DatumArea.X3Y;
            LeftBottomX = poiParam.DatumArea.X4X;
            LeftBottomY = poiParam.DatumArea.X4Y;
            IsDynamics = false;
            CfgJson = poiParam.CfgJson;
            CreateDate = DateTime.Now;
            IsEnable = true;
            IsDelete = false;
            TenantId = 0;
        }

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
        public bool? IsDynamics { get; set; } = false;
        public string? CfgJson { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public bool? IsEnable { get; set; } = true;
        public bool? IsDelete { get; set; } = false;
        public string? Remark { get; set; }
        public int TenantId { get; set; }
    }

    public class PoiMasterDao : BaseDaoMaster<PoiMasterModel>
    {
        public PoiMasterDao() : base("", "t_scgd_cfg_poi_master", "id", true)
        {

        }

        public override PoiMasterModel GetModel(DataRow item)
        {
            PoiMasterModel model = new PoiMasterModel
            {
                Id = item.Field<int>("id"),
                Name = item.Field<string?>("name"),
                Type = item.Field<sbyte>("type"),
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
                IsDynamics = item.Field<bool?>("dynamics"),
                CfgJson = item.Field<string?>("cfg_json"),
                CreateDate = item.Field<DateTime?>("create_date"),
                IsEnable = item.Field<bool?>("is_enable"),
                IsDelete = item.Field<bool?>("is_delete"),
                Remark = item.Field<string?>("remark"),
                TenantId = item.Field<int>("tenant_id"),
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
                row["tenant_id"] = item.TenantId;
            }
            return row;
        }

        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("name", typeof(string));
            dInfo.Columns.Add("type", typeof(sbyte));
            dInfo.Columns.Add("width", typeof(int));
            dInfo.Columns.Add("height", typeof(int));
            dInfo.Columns.Add("left_top_x", typeof(int));
            dInfo.Columns.Add("left_top_y", typeof(int));
            dInfo.Columns.Add("right_top_x", typeof(int));
            dInfo.Columns.Add("right_top_y", typeof(int));
            dInfo.Columns.Add("right_bottom_x", typeof(int));
            dInfo.Columns.Add("right_bottom_y", typeof(int));
            dInfo.Columns.Add("left_bottom_x", typeof(int));
            dInfo.Columns.Add("left_bottom_y", typeof(int));
            dInfo.Columns.Add("dynamics", typeof(bool));
            dInfo.Columns.Add("cfg_json", typeof(string));
            dInfo.Columns.Add("create_date", typeof(DateTime));
            dInfo.Columns.Add("is_enable", typeof(bool));
            dInfo.Columns.Add("is_delete", typeof(bool));
            dInfo.Columns.Add("remark", typeof(string));
            dInfo.Columns.Add("tenant_id", typeof(int));
            return dInfo;
        }
    }
}
