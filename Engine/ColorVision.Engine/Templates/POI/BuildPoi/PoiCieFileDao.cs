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
        [Column("id")]
        public int Id { get; set; }

        [Column("pid")]
        public int Pid  { get; set; }

        [Column("file_name")]
        public string FileName { get; set; }
        [Column("file_url")]
        public string FileUrl { get; set; }

        [Column("file_type")]
        public int FileType { get; set; }
    }

    public class PoiCieFileDao : BaseTableDao<PoiCieFileModel>
    {
        public static PoiCieFileDao Instance { get; set; } = new PoiCieFileDao();

    }
}
