#pragma warning disable CA1822,CS0618
using ColorVision.Common.Utilities;
using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Threading.Tasks;
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

    public class PhyLicenseDao : LocalConfigurationDao<LicenseModel>
    {
        public PhyLicenseDao(LocalTemplateStore? store = null, Func<bool>? isConnected = null) : base("device-license", store, isConnected) { }
        public static PhyLicenseDao Instance { get; set; } = new PhyLicenseDao();

        public async Task<int> DeleteExpiredAsync(int[] licenseIds, DateTime cutoff)
        {
            int deleted = 0;
            foreach (int id in System.Linq.Enumerable.Distinct(licenseIds))
            {
                if (IsLocalId(id) && GetById(id) is LicenseModel model && DeleteUnchanged(model, current => current.ExpiryDate != null && current.ExpiryDate < cutoff)) deleted++;
            }
            int[] remoteIds = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(licenseIds, id => id > 0));
            if (remoteIds.Length == 0) return deleted;
            if (UseLocal) throw new InvalidOperationException("MySQL 未连接，不能删除服务器许可证。");
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            return deleted + await DeleteExpiredAsync(db, remoteIds, cutoff);
        }

        internal static Task<int> DeleteExpiredAsync(ISqlSugarClient db, int[] licenseIds, DateTime cutoff)
        {
            if (licenseIds.Length == 0)
                return Task.FromResult(0);

            // Recheck expiration when deleting: a license may have been renewed since confirmation.
            return db.Deleteable<LicenseModel>()
                .In(licenseIds)
                .Where(x => x.ExpiryDate != null && x.ExpiryDate < cutoff)
                .ExecuteCommandAsync();
        }

        public LicenseModel? GetByMAC(string Code, bool? local = null)
        {
           if (local ?? UseLocal) return System.Linq.Enumerable.FirstOrDefault(GetLocal(), model => string.Equals(model.MacAddress, Code, StringComparison.OrdinalIgnoreCase));
           using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
           return  Db.Queryable<LicenseModel>().Where(x => x.MacAddress == Code).First();
        }

    }


}
