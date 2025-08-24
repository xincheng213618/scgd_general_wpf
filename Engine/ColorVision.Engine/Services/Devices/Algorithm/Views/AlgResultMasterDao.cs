#pragma warning disable CS8601,CS8603
using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    [@SugarTable("t_scgd_algorithm_result_master")]
    public class AlgResultMasterModel : PKModel
    {
        public AlgResultMasterModel() { }

        [SugarColumn(ColumnName ="tid")]
        public int? TId { get; set; }

        [SugarColumn(ColumnName ="tname")]
        public string TName { get; set; }

        [SugarColumn(ColumnName ="img_file")]
        public string ImgFile { get; set; }

        [SugarColumn(ColumnName ="img_file_type")]
        public AlgorithmResultType ImgFileType { get; set; }

        [SugarColumn(ColumnName ="version")]
        public string version { get; set; }

        [SugarColumn(ColumnName ="batch_id")]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName ="params")]
        public string Params { get; set; }

        [SugarColumn(ColumnName ="result_code")]
        public int? ResultCode { get; set; }

        [SugarColumn(ColumnName ="result")]
        public string Result { get; set; }

        [SugarColumn(ColumnName ="img_result")]
        public string ResultImagFile { get; set; }

        [SugarColumn(ColumnName ="total_time")]
        public long TotalTime { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; }

    }


    public class AlgResultMasterDao : BaseTableDao<AlgResultMasterModel>
    {
        public static AlgResultMasterDao Instance { get; set; } = new AlgResultMasterDao();
    }
}
