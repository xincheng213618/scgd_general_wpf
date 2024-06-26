﻿#pragma warning disable 
using ColorVision;
using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Dao
{
    public class SpectumResultDao : BaseTableDao<SpectumResultModel>
    {
        public static SpectumResultDao Instance { get; set; } = new SpectumResultDao();

        public SpectumResultDao() : base("t_scgd_measure_result_spectrometer", "id")
        {

        }

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
