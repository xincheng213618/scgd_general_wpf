﻿#pragma warning disable CS0618
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.UI.Sorts;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{

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

        public PoiCieFileDao() : base("t_scgd_algorithm_result_detail_poi_cie_file", "id")
        {

        }    
    }
}