using ColorVision.Engine.MySql.ORM;
using SqlSugar;

namespace ColorVision.Engine.Templates.POI.Dao
{
    [SugarTable("t_scgd_algorithm_poi_template_detail")]
    public class PoiDetailModel : PKModel
    {
        [SugarColumn(ColumnName ="name")]
        public string? Name { get; set; }
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }
        [SugarColumn(ColumnName ="pt_type")]
        public RiPointTypes Type { get; set; }
        [SugarColumn(ColumnName ="pix_x")]
        public int? PixX { get; set; }
        [SugarColumn(ColumnName ="pix_y")]
        public int? PixY { get; set; }
        [SugarColumn(ColumnName ="pix_width")]
        public int? PixWidth { get; set; }
        [SugarColumn(ColumnName ="pix_height")]
        public int? PixHeight { get; set; }
        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get; set; }

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
    }


    public class PoiDetailDao : BaseTableDao<PoiDetailModel>
    {
        public static PoiDetailDao Instance { get; } = new PoiDetailDao();
    }
}
