using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Types;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.PhySpectrums
{
    internal static class PhySpectrumStore
    {
        private static SqlSugarClient OpenDatabase()
        {
            if (!MySqlControl.GetInstance().IsConnect)
                throw new InvalidOperationException(Properties.Resources.SpectrumDatabaseRequired);
            return new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true });
        }

        internal static IReadOnlyList<PhySpectrum> Load(bool? useLocal = null)
        {
            bool local = useLocal ?? SysResourceDao.Instance.UseLocal;
            using var db = local ? null : OpenDatabase();
            var resources = local ? SysResourceDao.Instance.GetLocal()
                : db!.Queryable<SysResourceModel>().Where(r => r.Type == (int)ServiceTypes.PhySpectrums || r.Type == (int)ServiceTypes.Spectrum).ToList();
            resources = resources.Where(r => !r.IsDelete).ToList();
            var licenses = local ? PhyLicenseDao.Instance.GetLocal()
                : db!.Queryable<LicenseModel>().Where(l => l.LiceType == 1).ToList();
            var configured = new List<string>();
            foreach (var resource in resources.Where(r => r.Type == (int)ServiceTypes.Spectrum && !string.IsNullOrWhiteSpace(r.Value)))
            {
                // Read only the existing SN field; loading the catalog must not instantiate devices.
                try
                {
                    var sn = JObject.Parse(resource.Value!).GetValue("SN", StringComparison.OrdinalIgnoreCase)?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(sn)) configured.Add(sn);
                }
                catch (Newtonsoft.Json.JsonException) { /* An unrelated malformed device config does not hide other serials. */ }
            }
            return Merge(resources, licenses, configured);
        }

        internal static IReadOnlyList<PhySpectrum> Merge(IEnumerable<SysResourceModel> resources, IEnumerable<LicenseModel> licenses, IEnumerable<string> configured)
        {
            var physical = resources.Where(r => r.Type == (int)ServiceTypes.PhySpectrums && !r.IsDelete && !string.IsNullOrWhiteSpace(r.Code))
                .GroupBy(r => r.Code!.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.OrderBy(r => r.Id).First(), StringComparer.OrdinalIgnoreCase);
            var licenseBySn = licenses.Where(l => l.LiceType == 1 && !string.IsNullOrWhiteSpace(l.MacAddress))
                .GroupBy(l => l.MacAddress!.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.Id).First(), StringComparer.OrdinalIgnoreCase);
            return physical.Keys.Concat(licenseBySn.Keys).Concat(configured.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Select(sn => new PhySpectrum { SN = sn, ResourceId = physical.GetValueOrDefault(sn)?.Id, License = licenseBySn.GetValueOrDefault(sn) }).ToArray();
        }

        internal static void Register(string sn, bool? useLocal = null)
        {
            sn = NormalizeSerial(sn);
            if (useLocal ?? SysResourceDao.Instance.UseLocal)
            {
                var resource = SysResourceDao.Instance.GetLocal().FirstOrDefault(r => r.Type == (int)ServiceTypes.PhySpectrums
                    && string.Equals(r.Code?.Trim(), sn, StringComparison.OrdinalIgnoreCase));
                resource ??= new SysResourceModel { Code = sn, Name = sn, Type = (int)ServiceTypes.PhySpectrums };
                resource.IsDelete = false;
                SysResourceDao.Instance.Save(resource, local: true);
                return;
            }
            using var db = OpenDatabase();
            var existing = db.Queryable<SysResourceModel>().Where(r => r.Type == (int)ServiceTypes.PhySpectrums).ToList()
                .FirstOrDefault(r => string.Equals(r.Code?.Trim(), sn, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (existing.IsDelete)
                    db.Updateable<SysResourceModel>().SetColumns(r => r.IsDelete == false).Where(r => r.Id == existing.Id).ExecuteCommand();
                return;
            }
            db.Insertable(new SysResourceModel { Code = sn, Name = sn, Type = (int)ServiceTypes.PhySpectrums }).ExecuteCommand();
        }

        internal static string NormalizeSerial(string sn)
        {
            sn = sn.Trim();
            if (sn.Length == 0 || sn.Length > 255 || sn.Any(char.IsControl) || sn.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new ArgumentException(Properties.Resources.SpectrumInvalidSerial);
            return sn;
        }

        internal static void SaveLicense(LicenseModel license, bool? useLocal = null)
        {
            bool local = PhyLicenseDao.IsLocalId(license.Id) || (useLocal ?? PhyLicenseDao.Instance.UseLocal);
            if (local && license.Id > 0) throw new InvalidOperationException("MySQL 未连接，不能修改服务器许可证。");
            using var db = local ? null : OpenDatabase();
            var existing = local ? PhyLicenseDao.Instance.GetLocal().Where(l => string.Equals(l.MacAddress, license.MacAddress, StringComparison.OrdinalIgnoreCase)).ToList()
                : db!.Queryable<LicenseModel>().Where(l => l.MacAddress == license.MacAddress).ToList();
            var spectrumLicense = existing.FirstOrDefault(l => l.LiceType == 1);
            // Preserve existing IDs and associations. Never overwrite a camera license with the same SN.
            if (existing.Any(l => l.LiceType != 1))
                throw new InvalidOperationException(Properties.Resources.SpectrumLicenseTypeConflict);
            if (spectrumLicense == null)
            {
                if (local) PhyLicenseDao.Instance.Save(license, local: true);
                else db!.Insertable(license).ExecuteCommand();
                return;
            }
            spectrumLicense.LicenseValue = license.LicenseValue;
            spectrumLicense.Model = license.Model;
            spectrumLicense.CusTomerName = license.CusTomerName;
            spectrumLicense.ExpiryDate = license.ExpiryDate;
            if (local) PhyLicenseDao.Instance.Save(spectrumLicense, local: true);
            else db!.Updateable(spectrumLicense).ExecuteCommand();
        }
    }
}
