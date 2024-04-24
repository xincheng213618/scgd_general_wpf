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

namespace ColorVision.Services.PhyCameras.Dao
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
        public CameraLicenseModel()
        {
            CreateDate = DateTime.Now;
            ExpiryDate = DateTime.Now;
        }
        public string? Code { get; set; }
        public int? DevCameraId { get; set; }

        public int? DevCaliId { get; set; }

        public string? LicenseValue { get; set; }

        public string? LicenseContent { get => Tool.Base64Decode(LicenseValue?? string.Empty); }

        public ColorVisionLincense ColorVisionLincense { get => JsonConvert.DeserializeObject<ColorVisionLincense>(LicenseContent??string.Empty)?? new ColorVisionLincense(); }

        public string? Model { get; set; }

        public string? MacAddress { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public string? CusTomerName { get; set; }
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
            dataTable.Columns.Add("create_date", typeof(DateTime));
            dataTable.Columns.Add("expired", typeof(DateTime));
            dataTable.Columns.Add("code", typeof(string));
            dataTable.Columns.Add("res_dev_cam_pid", typeof(int));
            dataTable.Columns.Add("res_dev_cali_pid", typeof(int));
            return dataTable;
        }

        public override CameraLicenseModel GetModelFromDataRow(DataRow item) => new CameraLicenseModel()
        {
            Id = item.Field<int>("id"),
            LicenseValue = item.Field<string?>("value"),
            Model = item.Field<string?>("model"),
            MacAddress = item.Field<string?>("mac_sn"),
            CusTomerName = item.Field<string?>("customer_name"),
            CreateDate = item.Field<DateTime>("create_date"),
            ExpiryDate = item.Field<DateTime?>("expired"),
            Code = item.Field<string?>("code"),
            DevCameraId = item.Field<int?>("res_dev_cam_pid"),
            DevCaliId = item.Field<int?>("res_dev_cali_pid")
        };

        public override DataRow Model2Row(CameraLicenseModel item, DataRow row)
        {
            if (item != null)
            {
                row["id"] = item.Id;
                row["value"] = item.LicenseValue;
                row["model"] = item.Model;
                row["mac_sn"] = item.MacAddress;
                row["customer_name"] = item.CusTomerName;
                row["create_date"] = item.CreateDate;
                row["expired"] = item.ExpiryDate;
                row["code"] = item.Code;
                if (item.DevCameraId != null)
                    row["res_dev_cam_pid"] = item.DevCameraId;
                if (item.DevCaliId!=null)
                    row["res_dev_cali_pid"] = item.DevCaliId;
            }
            return row;
        }

        public CameraLicenseModel? GetByMAC(string? Code) => GetByParam(new Dictionary<string, object>() { { "mac_sn", Code } });
    
    }
}
