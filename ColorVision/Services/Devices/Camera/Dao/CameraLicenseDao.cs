#pragma warning disable CS0618
using ColorVision.Common.Utilities;
using ColorVision.Common.MVVM;
using ColorVision.MySql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Newtonsoft.Json;
using ColorVision.Common.Sorts;
using ColorVision.MySql.ORM;

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
        public int PKId { get => Id; set => Id = value; }
        public int IdShow { get; set; }


        public CameraLicenseModel()
        {
            CreateDate = DateTime.Now;
            ExpiryDate = DateTime.Now;
        }

        public int? RescourceId { get; set; }

        public string? LicenseValue { get; set; }

        public string? LicenseContent { get => Tool.Base64Decode(LicenseValue?? string.Empty); }

        public ColorVisionLincense ColorVisionLincense { get => JsonConvert.DeserializeObject<ColorVisionLincense>(LicenseContent??string.Empty)?? new ColorVisionLincense(); }

        public string? Model { get; set; }

        public string? MacAddress { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public string? CusTomerName { get; set; }

        public string? CamerID { get; set; }
        public string? Config { get; set; }
        public string? CameraMode { get; set; }
        public DateTime? CreateDate { get; set; }
    }





    public class CameraLicenseDao : BaseTableDao<CameraLicenseModel>
    {
        public static CameraLicenseDao Instance { get; set; } = new CameraLicenseDao();

        public CameraLicenseDao() : base("t_scgd_camera_license", "id")
        {

        }

        public override DataTable CreateColumns(DataTable dataTable)
        {
            dataTable.Columns.Add("id", typeof(int));
            dataTable.Columns.Add("customer_name", typeof(string));
            dataTable.Columns.Add("mac_sn", typeof(string));
            dataTable.Columns.Add("model", typeof(string));
            dataTable.Columns.Add("value", typeof(string));
            dataTable.Columns.Add("pid", typeof(int));
            dataTable.Columns.Add("create_date", typeof(DateTime));
            dataTable.Columns.Add("expired", typeof(DateTime));
            return dataTable;
        }

        public override CameraLicenseModel GetModelFromDataRow(DataRow item)
        {
            CameraLicenseModel model = new CameraLicenseModel
            {
                Id = item.Field<int>("id"),
                RescourceId = item.Field<int>("pid"),
                LicenseValue = item.Field<string?>("value"),
                Model = item.Field<string?>("model"),
                MacAddress = item.Field<string?>("mac_sn"),
                CusTomerName = item.Field<string?>("customer_name"),
                CamerID = item.Field<string?>("phy_camera_id"),
                Config = item.Field<string?>("phy_camera_cfg"),
                CameraMode = item.Field<string?>("phy_camera_model"),
                CreateDate = item.Field<DateTime>("create_date"),
                ExpiryDate = item.Field<DateTime?>("expired")
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
                row["expired"] = item.ExpiryDate;
            }
            return row;
        }

        public List<CameraLicenseModel> GetAllByMAC(string id ,int pid) => GetAllByParam(new Dictionary<string, object>() { { "mac_sn", id },{ "pid", pid } });


        public CameraLicenseModel? GetLatestCameraTemp(int? resId = null)
        {
            return GetCameraTempsByCreateDate(resId, limit: 1).FirstOrDefault();
        }
         
        public List<string?> GetAllCameraID() => GetAll().Where(x => !string.IsNullOrEmpty(x.CamerID)).Select(x => x.CamerID).ToList();

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
