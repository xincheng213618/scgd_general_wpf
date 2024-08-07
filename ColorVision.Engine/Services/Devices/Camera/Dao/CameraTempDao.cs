﻿using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Dao
{
    public class CameraTempModel : PKModel
    {
        public float? TempValue { get; set; }
        public int? PwmValue { get; set; }
        public DateTime? CreateDate { get; set; }

        public int? RescourceId { get; set; }
    }

    public class CameraTempDao : BaseTableDao<CameraTempModel>
    {
        public static CameraTempDao Instance { get; set; } = new CameraTempDao();

        public CameraTempDao() : base("t_scgd_camera_temp", "id")
        {
        }

        public override DataTable CreateColumns(DataTable dataTable)
        {
            dataTable.Columns.Add("id", typeof(int));
            dataTable.Columns.Add("temp_value", typeof(float));
            dataTable.Columns.Add("pwm_value", typeof(int));
            dataTable.Columns.Add("create_date", typeof(DateTime));
            dataTable.Columns.Add("res_id", typeof(int));
            return dataTable;
        }

        public override CameraTempModel GetModelFromDataRow(DataRow item)
        {
            CameraTempModel model = new()
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
            List<CameraTempModel> list = new();
            DataTable dInfo;
            string sql;
            if (resId.HasValue)
            {
                sql = $"SELECT id, temp_value, pwm_value,create_date,res_id FROM {TableName} WHERE res_id = @ResId ORDER BY create_date DESC LIMIT @Limit";
                var parameters = new Dictionary<string, object>
                {
                    {"@ResId", resId.Value},
                    {"@Limit", limit}
                };
                dInfo = GetData(sql, parameters);
            }
            else
            {

                sql = $"SELECT id, temp_value, pwm_value,create_date,res_id FROM {TableName} ORDER BY create_date DESC LIMIT @Limit";
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
