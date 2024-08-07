﻿#pragma warning disable CS0618
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.PhyCameras.Dao
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
        [Column("id")]
        public int Id { get; set; }
        public CameraLicenseModel()
        {
            CreateDate = DateTime.Now;
            ExpiryDate = DateTime.Now;
        }
        [Column("res_dev_cam_pid")]
        public int? DevCameraId { get; set; }
        [Column("res_dev_cali_pid")]
        public int? DevCaliId { get; set; }

        [Column("value")]
        public string? LicenseValue { get; set; }

        [ColumnIgnore]
        public string? LicenseContent { get => Tool.Base64Decode(LicenseValue?? string.Empty); }
        [ColumnIgnore]
        public ColorVisionLincense ColorVisionLincense { get => JsonConvert.DeserializeObject<ColorVisionLincense>(LicenseContent??string.Empty)?? new ColorVisionLincense(); }
        [Column("model")]
        public string? Model { get; set; }
        [Column("mac_sn")]
        public string? MacAddress { get; set; }
        [Column("expired")]
        public DateTime? ExpiryDate { get; set; }
        [Column("customer_name")]
        public string? CusTomerName { get; set; }
        [Column("create_date")]
        public DateTime? CreateDate { get; set; }
    }

    public class CameraLicenseDao : BaseTableDao<CameraLicenseModel>
    {
        public static CameraLicenseDao Instance { get; set; } = new CameraLicenseDao();

        public CameraLicenseDao() : base("t_scgd_camera_license", "id")
        {

        }

        public CameraLicenseModel? GetByMAC(string Code) => GetByParam(new Dictionary<string, object>() { { "mac_sn", Code } });
    
    }
}
