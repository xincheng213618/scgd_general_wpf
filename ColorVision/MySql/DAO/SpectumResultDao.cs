#pragma warning disable 
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.MySql.DAO
{
    public class SpectumResultModel : PKModel
    {
        public float? IntTime { get; set; }
        public int iAveNum { get; set; }
        public bool IsUseAutoIntTime { get; set; }
        public bool IsUseAutoDark { get; set; }
        public int? Pid { get; set; }
        public int? BatchId { get; set; }
        public string? fPL { get; set; }
        public string? fRi { get; set; }
        public float? fx { get; set; }
        public float? fy { get; set; }
        public float? fu { get; set; }
        public float? fv { get; set; }
        public float? fCCT { get; set; }
        public float? dC { get; set; }
        public float? fLd { get; set; }
        public float? fPur { get; set; }
        public float? fLp { get; set; }
        public float? fHW { get; set; }
        public float? fLav { get; set; }
        public float? fRa { get; set; }
        public float? fRR { get; set; }
        public float? fGR { get; set; }
        public float? fBR { get; set; }
        public float? fIp { get; set; }
        public float? fPh { get; set; }
        public float? fPhe { get; set; }
        public float? fPlambda { get; set; }
        public float? fSpect1 { get; set; }
        public float? fSpect2 { get; set; }
        public float? fInterval { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }
    public class SpectumResultDao : BaseDaoMaster<SpectumResultModel>
    {
        public SpectumResultDao() : base(string.Empty, "t_scgd_measure_result_spectrometer", "id", false)
        {
        }

        public List<SpectumResultModel> selectBySN(string sn)
        {
            List<SpectumResultModel> list = new List<SpectumResultModel>();
            DataTable d_info = GetTableAllBySN(sn);
            foreach (var item in d_info.AsEnumerable())
            {
                SpectumResultModel? model = GetModel(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

        private DataTable GetTableAllBySN(string bid)
        {
            string sql = $"select * from {GetTableName()} where batch_id='{bid}'";
            DataTable d_info = GetData(sql);
            return d_info;
        }

        public override SpectumResultModel GetModel(DataRow item)
        {
            SpectumResultModel model = new SpectumResultModel
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

            return model;
        }
    }
}
