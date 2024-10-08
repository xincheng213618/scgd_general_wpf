#pragma warning disable CS0618
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi
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
