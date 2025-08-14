#pragma warning disable CS8601,CS8603
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    [SugarTable("t_scgd_algorithm_result_master")]
    [Table("t_scgd_algorithm_result_master")]
    public class AlgResultMasterModel : PKModel
    {
        public AlgResultMasterModel() { }

        [SugarColumn(ColumnName = "tid")]
        [Column("tid")]
        public int? TId { get; set; }

        [SugarColumn(ColumnName = "tname")]
        [Column("tname")]
        public string TName { get; set; }

        [SugarColumn(ColumnName = "img_file")]
        [Column("img_file")]
        public string ImgFile { get; set; }

        [SugarColumn(ColumnName = "img_file_type")]
        [Column("img_file_type")]
        public AlgorithmResultType ImgFileType { get; set; }

        [SugarColumn(ColumnName = "version")]
        [Column("version")]
        public string version { get; set; }

        [SugarColumn(ColumnName = "batch_id")]
        [Column("batch_id")]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName = "params")]
        [Column("params")]
        public string Params { get; set; }

        [SugarColumn(ColumnName = "result_code")]
        [Column("result_code")]
        public int? ResultCode { get; set; }

        [SugarColumn(ColumnName = "result")]
        [Column("result")]
        public string Result { get; set; }

        [SugarColumn(ColumnName = "img_result")]
        [Column("img_result")]
        public string ResultImagFile { get; set; }

        [SugarColumn(ColumnName = "total_time")]
        [Column("total_time")]
        public long TotalTime { get; set; }

        [SugarColumn(ColumnName = "create_date")]
        [Column("create_date")]
        public DateTime? CreateDate { get; set; }

    }


    public class AlgResultMasterDao : BaseTableDao<AlgResultMasterModel>
    {
        public static AlgResultMasterDao Instance { get; set; } = new AlgResultMasterDao();
    }
}
