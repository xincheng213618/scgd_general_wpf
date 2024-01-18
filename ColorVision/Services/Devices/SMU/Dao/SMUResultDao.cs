using ColorVision.MySql;
using ColorVision.Services.Devices.SMU.Configs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Services.Devices.SMU.Dao
{
    public class SMUResultModel : PKModel
    {
        public SMUResultModel() { }
        public SMUResultModel(string serialNumber, double rstV, double rstI, SMUGetDataParam param)
        {
            Bid = serialNumber;
            setValue(rstV, rstI, param);
        }

        public SMUResultModel(int pid, double rstV, double rstI, SMUGetDataParam param)
        {
            Pid = pid;
            setValue(rstV, rstI, param);
        }

        private void setValue(double rstV, double rstI, SMUGetDataParam param)
        {
            IsSourceV = param.IsSourceV;
            SrcValue = (float)param.MeasureValue;
            LimitValue = (float)param.LimitValue;
            VResult = (float)rstV;
            IResult = (float)rstI;
            CreateDate = DateTime.Now;
        }

        public int? Pid { get; set; }//
        public string? Bid { get; set; }//
        public bool IsSourceV { get; set; }//是否电压
        public float SrcValue { get; set; }//源值
        public float LimitValue { get; set; }//限值
        public float VResult { get; set; }//电压
        public float IResult { get; set; }//电流
        public DateTime? CreateDate { get; set; }
    }
    public class SMUResultDao : BaseDaoMaster<SMUResultModel>
    {
        public SMUResultDao() : base(string.Empty, "t_scgd_measure_result_smu", "id", false)
        {
        }
        public override DataRow Model2Row(SMUResultModel item, DataRow row)
        {
            if (item != null)
            {
                if (item.Id > 0) row["id"] = item.Id;
                //
                if (string.IsNullOrEmpty(item.Bid))
                    row["pid"] = item.Pid;
                else
                    row["batch_id"] = item.Bid;

                row["is_source_v"] = item.IsSourceV;

                row["src_value"] = item.SrcValue;
                row["limit_value"] = item.LimitValue;
                //
                row["v_result"] = item.VResult;
                row["i_result"] = item.IResult;
                row["create_date"] = item.CreateDate;
            }
            return row;
        }

        public List<SMUResultModel> selectBySN(string sn)
        {
            List<SMUResultModel> list = new List<SMUResultModel>();
            DataTable d_info = GetTableAllBySN(sn);
            foreach (var item in d_info.AsEnumerable())
            {
                SMUResultModel? model = GetModel(item);
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

        public override SMUResultModel GetModel(DataRow item)
        {
            SMUResultModel model = new SMUResultModel
            {
                Id = item.Field<int>("id"),
                Pid = item.Field<int?>("pid"),
                Bid = item.Field<string>("batch_id"),
                IsSourceV = item.Field<bool>("is_source_v"),
                SrcValue = item.Field<float>("src_value"),
                LimitValue = item.Field<float>("limit_value"),
                VResult = item.Field<float>("v_result"),
                IResult = item.Field<float>("i_result"),
                CreateDate = item.Field<DateTime?>("create_date"),
            };

            return model;
        }
    }
}
