#pragma warning disable CS0618
using ColorVision.Common.Utilities;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Sorts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ColorVision.Services.Devices.Camera.Dao
{
    public class ColorVisionLincense
    {
        [JsonProperty("authority_signature")]
        public string AuthoritySignature { get; set; }

        [JsonProperty("device_mode")]
        public string DeviceMode { get; set; }

        [JsonProperty("expiry_date")]
        public string ExpiryDate { get; set; }
        public DateTime ExpiryDateTime { get => TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)).AddSeconds(int.Parse(ExpiryDate)); }

        [JsonProperty("issue_date")]
        public string IssueDate { get; set; }

        [JsonProperty("issuing_authority")]
        public string IssuingAuthority { get; set; }

        [JsonProperty("licensee")]
        public string Licensee { get; set; }

        [JsonProperty("licensee_signature")]
        public string LicenseeSignature { get; set; }
    }

    public class CameraLicenseModel : ViewModelBase,IPKModel, ISortID
    {
        public int Id { get; set; }

        public int GetPK() => Id;
        public void SetPK(int id) => Id = id;


        public CameraLicenseModel()
        {
            CreateDate = DateTime.Now;
        }

        public int? RescourceId { get; set; }

        public string? LicenseValue { get; set; }

        public string? LicenseContent { get => Tool.Base64Decode(LicenseValue?? string.Empty); }

        public ColorVisionLincense ColorVisionLincense { get => JsonConvert.DeserializeObject<ColorVisionLincense>(LicenseContent??string.Empty)?? new ColorVisionLincense(); }

        public string? Model { get; set; }

        public string? MacAddress { get; set; }

        public string? CusTomerName { get; set; }

        public DateTime? CreateDate { get; set; }
    }





    public class CameraLicenseDao : BaseTableDao<CameraLicenseModel>
    {
        public CameraLicenseDao() : base("t_scgd_camera_license", "id")
        {

        }

        public override DataTable CreateColumns(DataTable dInfo)
        {
            dInfo.Columns.Add("id", typeof(int));
            dInfo.Columns.Add("customer_name", typeof(string));
            dInfo.Columns.Add("mac_sn", typeof(string));
            dInfo.Columns.Add("model", typeof(string));
            dInfo.Columns.Add("value", typeof(string));
            dInfo.Columns.Add("pid", typeof(int));
            dInfo.Columns.Add("create_date", typeof(DateTime));
            return dInfo;
        }

        public override CameraLicenseModel GetModelFromDataRow(DataRow item)
        {
            CameraLicenseModel model = new CameraLicenseModel
            {
                Id = item.Field<int>("id"),
                RescourceId = item.Field<int>("pid"),
                LicenseValue = item.Field<string>("value"),
                Model = item.Field<string>("model"),
                MacAddress = item.Field<string>("mac_sn"),
                CusTomerName = item.Field<string>("customer_name"),
                CreateDate = item.Field<DateTime>("create_date"),
            };
            return model;
        }

        public override DataRow Model2Row(CameraLicenseModel item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["pid"] = item.RescourceId;
                row["value"] = item.LicenseValue;
                row["model"] = item.Model;
                row["mac_sn"] = item.MacAddress;
                row["customer_name"] = item.CusTomerName;
                row["create_date"] = item.CreateDate;
            }
            return row;
        }

        public CameraLicenseModel? GetLatestCameraTemp(int? resId = null)
        {
            return GetCameraTempsByCreateDate(resId, limit: 1).FirstOrDefault();
        }

        public List<CameraLicenseModel> GetCameraTempsByCreateDate(int? resId = null, int limit = 1)
        {
            List<CameraLicenseModel> list = new List<CameraLicenseModel>();
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
                var model = GetModelFromDataRow(item);
                if (model != null)
                {
                    list.Add(model);
                }
            }
            return list;
        }

    }








}
