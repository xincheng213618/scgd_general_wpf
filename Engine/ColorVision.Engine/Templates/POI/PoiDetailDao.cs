using ColorVision.Database;
using ColorVision.ImageEditor;
using SqlSugar;

namespace ColorVision.Engine.Templates.POI
{
    [SugarTable("t_scgd_algorithm_poi_template_detail")]
    public class PoiDetailModel : PKModel
    {

        public PoiDetailModel()
        {

        }

        public PoiDetailModel(int pid, PoiPoint data)
        {
            Id = data.Id;
            Pid = pid;
            Name = data.Name;
            Type = data.PointType;
            PixX = (int)data.PixX;
            PixY = (int)data.PixY;
            PixWidth = (int)data.PixWidth;
            PixHeight = (int)data.PixHeight;
        }


        [SugarColumn(ColumnName = "name", Length = 255, IsNullable = true)]
        public string? Name { get; set; }
        [SugarColumn(ColumnName ="pid", Length = 11, IsNullable = true)]
        public int? Pid { get; set; }
        [SugarColumn(ColumnName ="pt_type", Length = 11, IsNullable = true)]
        public GraphicTypes Type { get; set; }
        [SugarColumn(ColumnName ="pix_x", Length = 11, IsNullable = true)]
        public int? PixX { get; set; }
        [SugarColumn(ColumnName ="pix_y", Length = 11, IsNullable = true)]
        public int? PixY { get; set; }
        [SugarColumn(ColumnName ="pix_width", Length = 11, IsNullable = true)]
        public int? PixWidth { get; set; }
        [SugarColumn(ColumnName ="pix_height", Length = 11, IsNullable = true)]
        public int? PixHeight { get; set; }

        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get; set; } = true;

        [SugarColumn(ColumnName = "is_delete")]
        public bool IsDelete { get; set; }

        [SugarColumn(ColumnName ="remark",ColumnDataType = "text", IsNullable =true)]
        public string? Remark { get; set; }

    }
}
