#pragma warning disable 
using ColorVision;
using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Dao
{
    [Table("t_scgd_measure_result_spectrometer")]
    public class SpectumResultModel : PKModel
    {
        [Column("fIntTime")]
        public float? IntTime { get; set; }

        [Column("iAveNum")]
        public int iAveNum { get; set; }

        [Column("self_adaption_init_dark")]
        public bool IsUseAutoIntTime { get; set; }

        [Column("auto_init_dark")]
        public bool IsUseAutoDark { get; set; }

        [Column("pid")]
        public int? Pid { get; set; }

        [Column("batch_id")]
        public int? BatchId { get; set; }

        [Column("fPL")]
        public string? fPL { get; set; }

        [Column("fRi")]
        public string? fRi { get; set; }

        [Column("fx")]
        public float? fx { get; set; }

        [Column("fy")]
        public float? fy { get; set; }

        [Column("fu")]
        public float? fu { get; set; }

        [Column("fv")]
        public float? fv { get; set; }

        [Column("fCCT")]
        public float? fCCT { get; set; }

        [Column("dC")]
        public float? dC { get; set; }

        [Column("fLd")]
        public float? fLd { get; set; }

        [Column("fPur")]
        public float? fPur { get; set; }

        [Column("fLp")]
        public float? fLp { get; set; }

        [Column("fHW")]
        public float? fHW { get; set; }

        [Column("fLav")]
        public float? fLav { get; set; }

        [Column("fRa")]
        public float? fRa { get; set; }

        [Column("fRR")]
        public float? fRR { get; set; }

        [Column("fGR")]
        public float? fGR { get; set; }

        [Column("fBR")]
        public float? fBR { get; set; }

        [Column("fIp")]
        public float? fIp { get; set; }

        [Column("fPh")]
        public float? fPh { get; set; }

        [Column("fPhe")]
        public float? fPhe { get; set; }

        [Column("fPlambda")]
        public float? fPlambda { get; set; }

        [Column("fSpect1")]
        public float? fSpect1 { get; set; }

        [Column("fSpect2")]
        public float? fSpect2 { get; set; }

        [Column("fInterval")]
        public float? fInterval { get; set; }

        [Column("create_date")]
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
