using ColorVision.Template;
using System.Data;

namespace ColorVision.MySql.DAO
{
    public class PoiDetailModel : PKModel
    {
        public string? Name { get; set; }
        public int? Pid { get; set; }
        public int? Type { get; set; }
        public int? PixX { get; set; }
        public int? PixY { get; set; }
        public int? PixWidth { get; set; }
        public int? PixHeight { get; set; }
        public bool? IsEnable { get; set; }
        public bool? IsDelete { get; set; }
        public string? Remark { get; set; }

        public PoiDetailModel()
        {

        }

        public PoiDetailModel(int pid, PoiParamData data)
        {
            Id = data.ID;
            Pid = pid;
            Name = data.Name;
            Type = data.PointType switch
            {
                RiPointTypes.Circle => 0,
                RiPointTypes.Rect => 1,
                RiPointTypes.Mask => 2,
                _ => (int?)0,
            };
            PixX = (int)data.PixX;
            PixY = (int)data.PixY;
            PixWidth = (int)data.PixWidth;
            PixHeight = (int)data.PixHeight;
            IsEnable = true;
            IsDelete = false;
        }
    }


    public class PoiDetailDao : BaseDaoMaster<PoiDetailModel>
    {
        public PoiDetailDao() : base(string.Empty, "t_scgd_algorithm_poi_template_detail", "id", true)
        {
        }


        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("name", typeof(string));
            dInfo.Columns.Add("pt_type", typeof(sbyte));
            dInfo.Columns.Add("pid", typeof(int));
            dInfo.Columns.Add("pix_width", typeof(int));
            dInfo.Columns.Add("pix_height", typeof(int));
            dInfo.Columns.Add("pix_x", typeof(int));
            dInfo.Columns.Add("pix_y", typeof(int));
            //dataTable.Columns.Add("create_date", typeof(DateTime));
            dInfo.Columns.Add("is_enable", typeof(bool));
            dInfo.Columns.Add("is_delete", typeof(bool));
            dInfo.Columns.Add("remark", typeof(string));
            return dInfo;
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
                //CreateDate = item.Field<DateTime>("create_date"),
                IsEnable = item.Field<bool>("is_enable"),
                IsDelete = item.Field<bool>("is_delete"),
                Remark = item.Field<string>("remark"),
            };
            return model;
        }

        public override DataRow Model2Row(PoiDetailModel item, DataRow row)
        {
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
                //row["create_date"] = item.CreateDate;
                //row["is_enable"] = item.IsEnable;
                //row["is_delete"] = item.IsDelete;
                if (item.Remark != null) row["remark"] = item.Remark;
            }
            return row;
        }
    }
}
