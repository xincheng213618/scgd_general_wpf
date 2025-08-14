using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ColorVision.Engine.Services.Dao
{
    [SugarTable("t_scgd_camera_temp")]
    public class CameraTempModel : VPKModel
    {
        [SugarColumn(ColumnName ="temp_value")]
        public float? TempValue { get; set; }
        [SugarColumn(ColumnName ="pwm_value")]
        public int? PwmValue { get; set; }
        [SugarColumn(ColumnName ="create_date")]

        public DateTime? CreateDate { get; set; }

        [SugarColumn(ColumnName ="res_id")]
        public int? RescourceId { get; set; }
    }

    public class CameraTempDao : BaseTableDao<CameraTempModel>
    {
        public static CameraTempDao Instance { get; set; } = new CameraTempDao();

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
