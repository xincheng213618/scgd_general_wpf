using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ColorVision.MySql;

namespace ColorVision.Services.Dao
{
    public class CameraTempModel : PKModel
    {
        public float? TempValue { get; set; }
        public int? PwmValue { get; set; }
        public DateTime? CreateDate { get; set; }

        public int? RescourceId { get; set; }

    }

    public class CameraTempDao : BaseDaoMaster<CameraTempModel>
    {
        public CameraTempDao() : base(string.Empty, "t_scgd_camera_temp", "id", true)
        {
        }
        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("temp_value", typeof(float));
            dInfo.Columns.Add("pwm_value", typeof(int));
            dInfo.Columns.Add("create_date", typeof(DateTime));
            dInfo.Columns.Add("res_id", typeof(int));
            return dInfo;
        }

        public override CameraTempModel GetModelFromDataRow(DataRow item)
        {
            CameraTempModel model = new CameraTempModel
            {
                Id = item.Field<int>("id"),
                TempValue = item.Field<float>("temp_value"),
                PwmValue = item.Field<int>("pwm_value"),
                CreateDate = item.Field<DateTime>("create_date"),
                RescourceId = item.Field<int>("res_id")
            };
            return model;
        }

        public override DataRow Model2Row(CameraTempModel item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["temp_value"] = item.TempValue;
                row["pwm_value"] = item.PwmValue;
                row["create_date"] = item.CreateDate;
                row["res_id"] = item.RescourceId;
            }
            return row;
        }

        public CameraTempModel? GetLatestCameraTemp(int? resId = null)
        {
            return GetCameraTempsByCreateDate(resId, limit: 1).FirstOrDefault();
        }

        public List<CameraTempModel> GetCameraTempsByCreateDate(int? resId = null, int limit = 1)
        {
            List<CameraTempModel> list = new List<CameraTempModel>();
            DataTable dInfo;
            string sql;
            if (resId.HasValue)
            {
                sql = $"SELECT id, temp_value, pwm_value,create_date,res_id FROM {GetTableName()} WHERE res_id = @ResId ORDER BY create_date DESC LIMIT @Limit";
                var parameters = new Dictionary<string, object>
                {
                    {"@ResId", resId.Value},
                    {"@Limit", limit}
                };
                dInfo = GetData(sql, parameters);
            }
            else
            {

                sql = $"SELECT id, temp_value, pwm_value,create_date,res_id FROM {GetTableName()} ORDER BY create_date DESC LIMIT @Limit";
                var parameters = new Dictionary<string, object>
                {
                    {"@Limit", limit}
                };
                dInfo = GetData(sql, parameters);
            };

            foreach (DataRow item in dInfo.Rows)
            {
                CameraTempModel? model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }


    }




}
