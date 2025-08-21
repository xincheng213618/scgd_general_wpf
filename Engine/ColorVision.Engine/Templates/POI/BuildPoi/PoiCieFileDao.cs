#pragma warning disable CS0618
using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.Sorts;
using SqlSugar;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{

    [SugarTable("t_scgd_algorithm_result_detail_poi_cie_file")]
    public class PoiCieFileModel : ViewModelBase,IPKModel, ISortID, IViewResult
    {
        [SugarColumn(ColumnName ="id")]
        public int Id { get; set; }

        [SugarColumn(ColumnName ="pid")]
        public int Pid  { get; set; }

        [SugarColumn(ColumnName ="file_name")]
        public string FileName { get; set; }
        [SugarColumn(ColumnName ="file_url")]
        public string FileUrl { get; set; }

        [SugarColumn(ColumnName ="file_type")]
        public int FileType { get; set; }
    }

    public class PoiCieFileDao : BaseTableDao<PoiCieFileModel>
    {
        public static PoiCieFileDao Instance { get; set; } = new PoiCieFileDao();

    }
}
