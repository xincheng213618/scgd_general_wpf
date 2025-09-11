#pragma warning disable 
using ColorVision;
using ColorVision.Database;
using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Dao
{
    [SugarTable("t_scgd_measure_result_spectrometer")]
    public class SpectumResultModel : EntityBase, IInitTables
    {
        [SugarColumn(ColumnName = "device_code",IsNullable =true)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "batch_id", IsNullable = true)]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName = "z_index", IsNullable = true)]
        public int? Zindex { get; set; }

        [SugarColumn(ColumnName ="fIntTime")]
        public float? IntTime { get; set; }

        [SugarColumn(ColumnName ="iAveNum")]
        public int iAveNum { get; set; }

        [SugarColumn(ColumnName ="self_adaption_init_dark")]
        public bool IsUseAutoIntTime { get; set; }

        [SugarColumn(ColumnName ="auto_init_dark")]
        public bool IsUseAutoDark { get; set; }

        [SugarColumn(ColumnName = "self_adaption_init_dark",ColumnDescription = "自适应校零")]
        public bool self_adaption_init_dark { get; set; }

        [SugarColumn(ColumnName ="fPL")]
        public string? fPL { get; set; }

        [SugarColumn(ColumnName ="fRi")]
        public string? fRi { get; set; }

        [SugarColumn(ColumnName ="fx")]
        public float? fx { get; set; }

        [SugarColumn(ColumnName ="fy")]
        public float? fy { get; set; }

        [SugarColumn(ColumnName ="fu")]
        public float? fu { get; set; }

        [SugarColumn(ColumnName ="fv")]
        public float? fv { get; set; }

        [SugarColumn(ColumnName ="fCCT")]
        public float? fCCT { get; set; }

        [SugarColumn(ColumnName ="dC")]
        public float? dC { get; set; }

        [SugarColumn(ColumnName ="fLd")]
        public float? fLd { get; set; }

        [SugarColumn(ColumnName ="fPur")]
        public float? fPur { get; set; }

        [SugarColumn(ColumnName ="fLp")]
        public float? fLp { get; set; }

        [SugarColumn(ColumnName ="fHW")]
        public float? fHW { get; set; }

        [SugarColumn(ColumnName ="fLav")]
        public float? fLav { get; set; }

        [SugarColumn(ColumnName ="fRa")]
        public float? fRa { get; set; }

        [SugarColumn(ColumnName ="fRR")]
        public float? fRR { get; set; }

        [SugarColumn(ColumnName ="fGR")]
        public float? fGR { get; set; }

        [SugarColumn(ColumnName ="fBR")]
        public float? fBR { get; set; }

        [SugarColumn(ColumnName ="fIp")]
        public float? fIp { get; set; }

        [SugarColumn(ColumnName ="fPh")]
        public float? fPh { get; set; }

        [SugarColumn(ColumnName ="fPhe")]
        public float? fPhe { get; set; }

        [SugarColumn(ColumnName ="fPlambda")]
        public float? fPlambda { get; set; }

        [SugarColumn(ColumnName ="fSpect1")]
        public float? fSpect1 { get; set; }

        [SugarColumn(ColumnName ="fSpect2")]
        public float? fSpect2 { get; set; }

        [SugarColumn(ColumnName ="fInterval")]
        public float? fInterval { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }


}
