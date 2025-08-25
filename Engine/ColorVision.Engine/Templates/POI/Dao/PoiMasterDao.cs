using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.POI.Dao
{
    [SugarTable("t_scgd_algorithm_poi_template_master")]
    public class PoiMasterModel : PKModel
    {
        public PoiMasterModel()
        {
        }

        public PoiMasterModel(PoiParam poiParam)
        {
            Id = poiParam.Id;
            Name = poiParam.Name;
            Type = poiParam.Type;
            Width = poiParam.Width;
            Height = poiParam.Height;
            IsDynamics = poiParam.PoiConfig.IsPoiCIEFile;
            CfgJson = poiParam.CfgJson;
            CreateDate = DateTime.Now;
            IsEnable = true;
            IsDelete = false;
            TenantId = 0;

            LeftTopX = poiParam.LeftTopX;
            LeftTopY = poiParam.LeftTopY;
            RightTopX = poiParam.RightTopX;
            RightTopY = poiParam.RightTopY;
            RightBottomX = poiParam.RightBottomX;
            RightBottomY = poiParam.RightBottomY;
            LeftBottomX = poiParam.LeftBottomX;
            LeftBottomY = poiParam.LeftBottomY;
        }

        [SugarColumn(ColumnName ="name")]
        public string? Name { get; set; }

        [SugarColumn(ColumnName ="type")]
        public int? Type { get; set; }

        [SugarColumn(ColumnName ="width")]
        public int? Width { get; set; }

        [SugarColumn(ColumnName ="height")]
        public int? Height { get; set; }

        [SugarColumn(ColumnName ="left_top_x")]
        public int? LeftTopX { get; set; }

        [SugarColumn(ColumnName ="left_top_y")]
        public int? LeftTopY { get; set; }

        [SugarColumn(ColumnName ="right_top_x")]
        public int? RightTopX { get; set; }

        [SugarColumn(ColumnName ="right_top_y")]
        public int? RightTopY { get; set; }

        [SugarColumn(ColumnName ="right_bottom_x")]
        public int? RightBottomX { get; set; }

        [SugarColumn(ColumnName ="right_bottom_y")]
        public int? RightBottomY { get; set; }

        [SugarColumn(ColumnName ="left_bottom_x")]
        public int? LeftBottomX { get; set; }

        [SugarColumn(ColumnName ="left_bottom_y")]
        public int? LeftBottomY { get; set; }

        [SugarColumn(ColumnName ="dynamics")]
        public bool? IsDynamics { get; set; } = false;

        [SugarColumn(ColumnName ="cfg_json")]
        public string? CfgJson { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName ="is_enable")]
        public bool? IsEnable { get; set; } = true;

        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get; set; } = false;

        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get; set; }

        [SugarColumn(ColumnName ="tenant_id")]
        public int TenantId { get; set; }
    }

    public class PoiMasterDao : BaseTableDao<PoiMasterModel>
    {
        public static PoiMasterDao Instance { get; } = new PoiMasterDao();
    }
}
