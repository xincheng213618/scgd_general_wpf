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
    public class SpectumResultModel : EntityBase,IInitTables
    {
        [SugarColumn(ColumnName = "device_code", IsNullable = true, Length = 255)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "batch_id", IsNullable = true)]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName = "z_index", IsNullable = true)]
        public int? Zindex { get; set; }

        [SugarColumn(ColumnName = "smu_data_id", IsNullable = true, ColumnDescription = "SMU result ID")]
        public int? SmuDataId { get; set; }

        [SugarColumn(ColumnName = "fIntTime", IsNullable = true, ColumnDescription = "积分时间")]
        public float? IntTime { get; set; }

        [SugarColumn(ColumnName = "iAveNum", IsNullable = true, ColumnDescription = "平均次数")]
        public int? iAveNum { get; set; }

        [SugarColumn(ColumnName = "auto_integration", IsNullable = true, ColumnDescription = "自动积分")]
        public bool? AutoIntegration { get; set; }

        [SugarColumn(ColumnName = "auto_init_dark", IsNullable = true, ColumnDescription = "自动校零")]
        public bool? AutoInitDark { get; set; }

        [SugarColumn(ColumnName = "self_adaption_init_dark", IsNullable = true, ColumnDescription = "自适应校零")]
        public bool? SelfAdaptionInitDark { get; set; }

        [SugarColumn(ColumnName = "fPL", IsNullable = true, IsJson = true, ColumnDataType = "json", ColumnDescription = "相对光谱数据")]
        public string? fPL { get; set; }

        [SugarColumn(ColumnName = "fPL_file_name", IsNullable = true, Length =1024, ColumnDescription = "相对光谱数据文件(fPL为空)")]
        public string? fPL_file_name { get; set; }

        [SugarColumn(ColumnName = "fRi", IsNullable = true, IsJson = true, ColumnDataType = "json", ColumnDescription = "显色性指数 R1-R15")]
        public string? fRi { get; set; }

        [SugarColumn(ColumnName = "cie_data_ex", IsNullable = true, IsJson = true, ColumnDataType = "json", ColumnDescription = "CIE扩展数据")]
        public string? CieDataEx { get; set; }

        [SugarColumn(ColumnName = "fx", IsNullable = true, ColumnDescription = "色坐标x")]
        public float? fx { get; set; }

        [SugarColumn(ColumnName = "fy", IsNullable = true, ColumnDescription = "色坐标y")]
        public float? fy { get; set; }

        [SugarColumn(ColumnName = "fu", IsNullable = true, ColumnDescription = "色坐标u")]
        public float? fu { get; set; }

        [SugarColumn(ColumnName = "fv", IsNullable = true, ColumnDescription = "色坐标v")]
        public float? fv { get; set; }

        [SugarColumn(ColumnName = "fCCT", IsNullable = true, ColumnDescription = "相关色温(K)")]
        public float? fCCT { get; set; }

        [SugarColumn(ColumnName = "dC", IsNullable = true, ColumnDescription = "色差dC")]
        public float? dC { get; set; }

        [SugarColumn(ColumnName = "fLd", IsNullable = true, ColumnDescription = "主波长(nm)")]
        public float? fLd { get; set; }

        [SugarColumn(ColumnName = "fPur", IsNullable = true, ColumnDescription = "色纯度(%)")]
        public float? fPur { get; set; }

        [SugarColumn(ColumnName = "fLp", IsNullable = true, ColumnDescription = "峰值波长(nm)")]
        public float? fLp { get; set; }

        [SugarColumn(ColumnName = "fHW", IsNullable = true, ColumnDescription = "半波宽(nm)")]
        public float? fHW { get; set; }

        [SugarColumn(ColumnName = "fLav", IsNullable = true, ColumnDescription = "平均波长(nm)")]
        public float? fLav { get; set; }

        [SugarColumn(ColumnName = "fRa", IsNullable = true, ColumnDescription = "显色性指数 Ra")]
        public float? fRa { get; set; }

        [SugarColumn(ColumnName = "fRR", IsNullable = true, ColumnDescription = "红色比")]
        public float? fRR { get; set; }

        [SugarColumn(ColumnName = "fGR", IsNullable = true, ColumnDescription = "绿色比")]
        public float? fGR { get; set; }

        [SugarColumn(ColumnName = "fBR", IsNullable = true, ColumnDescription = "蓝色比")]
        public float? fBR { get; set; }

        [SugarColumn(ColumnName = "fIp", IsNullable = true, ColumnDescription = "峰值AD")]
        public float? fIp { get; set; }

        [SugarColumn(ColumnName = "fPh", IsNullable = true, ColumnDescription = "光度值")]
        public float? fPh { get; set; }

        [SugarColumn(ColumnName = "fPhe", IsNullable = true, ColumnDescription = "辐射度值")]
        public float? fPhe { get; set; }

        [SugarColumn(ColumnName = "fPlambda", IsNullable = true, ColumnDescription = "绝对光谱系数")]
        public float? fPlambda { get; set; }

        [SugarColumn(ColumnName = "fSpect1", IsNullable = true, ColumnDescription = "起始波长")]
        public float? fSpect1 { get; set; }

        [SugarColumn(ColumnName = "fSpect2", IsNullable = true, ColumnDescription = "结束波长")]
        public float? fSpect2 { get; set; }

        [SugarColumn(ColumnName = "fInterval", IsNullable = true, ColumnDescription = "波长间隔")]
        public float? fInterval { get; set; }

        [SugarColumn(ColumnName = "result_code", IsNullable = true, ColumnDescription = "结果CODE")]
        public int? ResultCode { get; set; }

        [SugarColumn(ColumnName = "total_time", IsNullable = true, ColumnDescription = "总用时(ms)")]
        public int? TotalTime { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = false, ColumnDescription = "创建日期", DefaultValue = "CURRENT_TIMESTAMP")]
        public DateTime CreateDate { get; set; }
    }


}
