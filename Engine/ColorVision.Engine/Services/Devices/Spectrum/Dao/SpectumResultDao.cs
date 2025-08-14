#pragma warning disable 
using ColorVision;
using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Dao
{
    [SugarTable("t_scgd_measure_result_spectrometer")]
    public class SpectumResultModel : PKModel
    {
        [SugarColumn(ColumnName ="fIntTime")]
        public float? IntTime { get; set; }

        [SugarColumn(ColumnName ="iAveNum")]
        public int iAveNum { get; set; }

        [SugarColumn(ColumnName ="self_adaption_init_dark")]
        public bool IsUseAutoIntTime { get; set; }

        [SugarColumn(ColumnName ="auto_init_dark")]
        public bool IsUseAutoDark { get; set; }

        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get; set; }

        [SugarColumn(ColumnName ="batch_id")]
        public int? BatchId { get; set; }

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


    public class SpectumResultDao : BaseTableDao<SpectumResultModel>
    {
        public static SpectumResultDao Instance { get; set; } = new SpectumResultDao();



        public List<SpectumResultModel> selectBySN(string sn)
        {
            List<SpectumResultModel> list = new List<SpectumResultModel>();
            DataTable d_info = GetTableAllBySN(sn);
            foreach (var item in d_info.AsEnumerable())
            {
                SpectumResultModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        private DataTable GetTableAllBySN(string bid)
        {
            string sql = $"select * from {TableName} where batch_id='{bid}'";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public List<SpectumResultModel> ConditionalQuery(string id, string batchid,DateTime? dateTimeSTART,DateTime? dateTimeEnd)
        {
            Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
            keyValuePairs.Add("id", id);
            keyValuePairs.Add("batch_id", batchid);
            keyValuePairs.Add(">create_date", dateTimeSTART);
            keyValuePairs.Add("<create_date", dateTimeEnd);
            return ConditionalQuery(keyValuePairs);
        }
        public override SpectumResultModel GetModelFromDataRow(DataRow item) => new SpectumResultModel
        {
            Id = item.Field<int>("id"),
            BatchId = item.Field<int>("batch_id"),
            CreateDate = item.Field<DateTime?>("create_date"),
            IntTime = item.Field<float?>("fIntTime"),
            iAveNum = item.Field<int>("iAveNum"),
            IsUseAutoIntTime = item.Field<bool>("self_adaption_init_dark"),
            IsUseAutoDark = item.Field<bool>("auto_init_dark"),
            fPL = item.Field<string>("fPL"),
            fRi = item.Field<string>("fRi"),
            fx = item.Field<float?>("fx"),
            fy = item.Field<float?>("fy"),
            fu = item.Field<float?>("fu"),
            fv = item.Field<float?>("fv"),
            fCCT = item.Field<float?>("fCCT"),
            dC = item.Field<float?>("dC"),
            fLd = item.Field<float?>("fLd"),
            fPur = item.Field<float?>("fPur"),
            fLp = item.Field<float?>("fLp"),
            fHW = item.Field<float?>("fHW"),
            fLav = item.Field<float?>("fLav"),
            fRa = item.Field<float?>("fRa"),
            fRR = item.Field<float?>("fRR"),
            fGR = item.Field<float?>("fGR"),
            fBR = item.Field<float?>("fBR"),
            fIp = item.Field<float?>("fIp"),
            fPh = item.Field<float?>("fPh"),
            fPhe = item.Field<float?>("fPhe"),
            fPlambda = item.Field<float?>("fPlambda"),
            fSpect1 = item.Field<float?>("fSpect1"),
            fSpect2 = item.Field<float?>("fSpect2"),
            fInterval = item.Field<float?>("fInterval"),
        };

    }
}
