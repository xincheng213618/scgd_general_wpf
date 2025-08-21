#pragma warning disable CS0618
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.PhyCameras.Dao
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
    public class LicenseModel : ViewModelBase,IPKModel, ISortID, IInitTables
    {
        [SugarColumn(ColumnName ="id")]
        public int Id { get; set; }
        public LicenseModel()
        {
            CreateDate = DateTime.Now;
            ExpiryDate = DateTime.Now;
        }

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
        public DateTime? ExpiryDate { get; set; }
        [SugarColumn(ColumnName ="customer_name")]
        public string? CusTomerName { get; set; }
        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; }
    }

    public class CameraLicenseDao : BaseTableDao<LicenseModel>
    {
        public static CameraLicenseDao Instance { get; set; } = new CameraLicenseDao();

        public LicenseModel? GetByMAC(string Code) => GetByParam(new Dictionary<string, object>() { { "mac_sn", Code } });
    
    }


}
