using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql.DAO
{
    public class SpectumResultModel : PKModel
    {
        public float IntTime { get; set; }
        public int AveNum { get; set; }
        public bool IsUseAutoIntTime { get; set; }
        public bool IsUseAutoDark { get; set; }
        public int? Pid { get; set; }
        public string? BatchId { get; set; }
        public string? PL { get; set; }
        public string? AbsPL { get; set; }
        public string? Ri { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float u { get; set; }
        public float v { get; set; }
        public float CCT { get; set; }
        public float dC { get; set; }
        public float Ld { get; set; }
        public float Pur { get; set; }
        public float Lp { get; set; }
        public float HW { get; set; }
        public float Lav { get; set; }
        public float Ra { get; set; }
        public float RR { get; set; }
        public float GR { get; set; }
        public float BR { get; set; }
        public float Ip { get; set; }
        public float Ph { get; set; }
        public float Phe { get; set; }
        public float Plambda { get; set; }
        public float Spect1 { get; set; }
        public float Spect2 { get; set; }
        public float Interval { get; set; }
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
                IntTime = item.Field<float>("fIntTime"),
                AveNum = item.Field<int>("iAveNum"),
                IsUseAutoIntTime = item.Field<bool>("bUseAutoIntTime"),
                IsUseAutoDark = item.Field<bool>("bUseAutoDark"),
                Pid = item.Field<int?>("pid"),
                BatchId = item.Field<string>("batch_id"),
                PL = item.Field<string>("fPL"),
                AbsPL = item.Field<string>("fAbsPL"),
                Ri = item.Field<string>("fRi"),
                x = item.Field<float>("fx"),
                y = item.Field<float>("fy"),
                u = item.Field<float>("fu"),
                v = item.Field<float>("fv"),
                CCT = item.Field<float>("fCCT"),
                dC = item.Field<float>("dC"),
                Ld = item.Field<float>("fLd"),
                Pur = item.Field<float>("fPur"),
                Lp = item.Field<float>("fLp"),
                HW = item.Field<float>("fHW"),
                Lav = item.Field<float>("fLav"),
                Ra = item.Field<float>("fRa"),
                RR = item.Field<float>("fRR"),
                GR = item.Field<float>("fGR"),
                BR = item.Field<float>("fBR"),
                Ip = item.Field<float>("fIp"),
                Ph = item.Field<float>("fPh"),
                Phe = item.Field<float>("fPhe"),
                Plambda = item.Field<float>("fPlambda"),
                Spect1 = item.Field<float>("fSpect1"),
                Spect2 = item.Field<float>("fSpect2"),
                Interval = item.Field<float>("fInterval"),
                CreateDate = item.Field<DateTime?>("create_date"),
            };

            return model;
        }
    }
}
