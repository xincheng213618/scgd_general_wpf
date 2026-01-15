#pragma warning disable CS0618
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using SqlSugar;
using System;
namespace ColorVision.Engine.Services.PhyCameras.Licenses
{
    public class ColorVisionLicense
    {
        [JsonProperty("authority_signature")]
        public string AuthoritySignature { get; set; }

        [JsonProperty("device_mode")]
        public string DeviceMode { get; set; }

        [JsonProperty("expiry_date")]
        public string ExpiryDate { get; set; }

        public DateTime ExpiryDateTime { get => ExpiryDate==null? DateTime.Now: TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)).AddSeconds(int.Parse(ExpiryDate)); }

        [JsonProperty("issue_date")]
        public string IssueDate { get; set; }

        [JsonProperty("issuing_authority")]
        public string IssuingAuthority { get; set; }

        [JsonProperty("licensee")]
        public string Licensee { get; set; }

        [JsonProperty("licensee_signature")]
        public string LicenseeSignature { get; set; }
    }

    [SugarTable("t_scgd_camera_license")]
    public class LicenseModel : ViewEntity
    {
        [SugarColumn(ColumnName ="res_dev_cam_pid")]
        public int? DevCameraId { get; set; }
        [SugarColumn(ColumnName ="res_dev_cali_pid")]
        public int? DevCaliId { get; set; }

        [SugarColumn(ColumnName ="lic_type")]
        public int LiceType { get; set; } = 0;

        [SugarColumn(ColumnName ="value")]
        public string? LicenseValue { get; set; }

        [@SugarColumn(IsIgnore = true)]
        public string? LicenseContent { get => Tool.Base64Decode(LicenseValue?? string.Empty); }
        [@SugarColumn(IsIgnore = true)]
        public ColorVisionLicense ColorVisionLicense { get => JsonConvert.DeserializeObject<ColorVisionLicense>(LicenseContent??string.Empty)?? new ColorVisionLicense(); }
        [SugarColumn(ColumnName ="model")]
        public string? Model { get; set; }
        [SugarColumn(ColumnName ="mac_sn")]
        public string? MacAddress { get; set; }
        [SugarColumn(ColumnName ="expired")]
        public DateTime? ExpiryDate { get; set; } = DateTime.Now;
        [SugarColumn(ColumnName ="customer_name")]
        public string? CusTomerName { get; set; }
        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }

    public class PhyLicenseDao : BaseTableDao<LicenseModel>
    {
        public static PhyLicenseDao Instance { get; set; } = new PhyLicenseDao();

        public LicenseModel? GetByMAC(string Code) => Db.Queryable<LicenseModel>().Where(x => x.MacAddress == Code).First();

    }


}
