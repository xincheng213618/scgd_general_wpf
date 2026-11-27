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
        private static SqlSugarClient OpenDatabase(MySqlConnectedEndpoint? endpoint)
        {
            endpoint ??= WorkstationConfiguration.ConnectedEndpoint;
            if (endpoint == null)
                throw new InvalidOperationException(Properties.Resources.SpectrumDatabaseRequired);
            if (!endpoint.IsCurrent)
                throw new InvalidOperationException(Properties.Resources.SpectrumCatalogSourceChanged);
            return endpoint.CreateDbClient();
        }

        internal static IReadOnlyList<PhySpectrum> Load(bool? useLocal = null, MySqlConnectedEndpoint? endpoint = null)
        {
            bool local = useLocal ?? SysResourceDao.Instance.UseLocal;
            using var db = local ? null : OpenDatabase(endpoint);
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

        internal static void Register(string sn, bool? useLocal = null, MySqlConnectedEndpoint? endpoint = null)
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
            using var db = OpenDatabase(endpoint);
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

        internal static void SaveLicense(LicenseModel license, bool? useLocal = null, MySqlConnectedEndpoint? endpoint = null)
        {
            bool local = PhyLicenseDao.IsLocalId(license.Id) || (useLocal ?? PhyLicenseDao.Instance.UseLocal);
            if (local && license.Id > 0) throw new InvalidOperationException("MySQL 未连接，不能修改服务器许可证。");
            using var db = local ? null : OpenDatabase(endpoint);
            var existing = local ? PhyLicenseDao.Instance.GetLocal().Where(l => string.Equals(l.MacAddress, license.MacAddress, StringComparison.OrdinalIgnoreCase)).ToList()
                : db!.Queryable<LicenseModel>().Where(l => l.MacAddress == license.MacAddress).ToList();
            // Preserve existing IDs and associations. Never overwrite a camera license with the same SN.
            if (existing.Any(l => l.LiceType != 1))
                throw new InvalidOperationException(Properties.Resources.SpectrumLicenseTypeConflict);
            if (existing.Count == 0)
            {
                if (local) PhyLicenseDao.Instance.Save(license, local: true);
                else db!.Insertable(license).ExecuteCommand();
                return;
            }
            // Legacy databases may have several rows for a serial. Catalogs and device connections
            // can select different rows, so renew every matching spectrum row without deleting associations.
            if (!local) { UpdateLicenseRecords(db!, license); return; }
            foreach (var spectrumLicense in existing)
            {
                spectrumLicense.LicenseValue = license.LicenseValue;
                spectrumLicense.Model = license.Model;
                spectrumLicense.CusTomerName = license.CusTomerName;
                spectrumLicense.ExpiryDate = license.ExpiryDate;
                PhyLicenseDao.Instance.Save(spectrumLicense, local: true);
            }
        }

        internal static int UpdateLicenseRecords(ISqlSugarClient db, LicenseModel license) => db.Updateable<LicenseModel>()
            .SetColumns(item => new LicenseModel
            {
                LicenseValue = license.LicenseValue,
                Model = license.Model,
                CusTomerName = license.CusTomerName,
                ExpiryDate = license.ExpiryDate
            })
            .Where(item => item.LiceType == 1 && item.MacAddress == license.MacAddress)
            .ExecuteCommand();

        internal static bool TryRemoveLicense(LicenseModel expected, MySqlConnectedEndpoint? endpoint)
        {
            if (endpoint == null) return PhyLicenseDao.Instance.TryRemoveSpectrumLicense(expected);

            using var db = OpenDatabase(endpoint);
            bool removed = TryRemoveLicenseRecords(db, expected, () => endpoint.IsCurrent);
            if (!removed && !endpoint.IsCurrent)
                throw new InvalidOperationException(Properties.Resources.SpectrumCatalogSourceChanged);
            return removed;
        }

        internal static bool TryRemoveLicenseRecords(ISqlSugarClient db, LicenseModel expected, Func<bool> isCurrent)
        {
            if (expected.Id <= 0 || !isCurrent()) return false;
            var ids = db.Queryable<LicenseModel>().Where(row => row.LiceType == 1)
                .Select(row => new { row.Id, row.MacAddress }).ToList()
                .Where(row => PhyLicenseDao.MatchesSpectrumSerial(row.MacAddress, expected.MacAddress)).Select(row => row.Id).ToArray();
            if (!ids.Contains(expected.Id)) return false;

            // Recheck the displayed license under a write lock before deleting the matching group.
            db.Ado.BeginTran();
            bool committed = false;
            try
            {
                var query = db.Queryable<LicenseModel>().In(ids);
                if (db.CurrentConnectionConfig.DbType == DbType.MySql) query = query.TranLock(DbLockType.Wait);
                var matches = query.ToList().Where(row => PhyLicenseDao.MatchesSpectrumSerial(row, expected.MacAddress)).ToArray();
                if (!matches.Any(row => row.Id == expected.Id && PhyLicenseDao.HasSameLicense(row, expected)) || !isCurrent())
                    return false;

                int[] matchingIds = matches.Select(row => row.Id).ToArray();
                if (db.Deleteable<LicenseModel>().In(matchingIds).Where(row => row.LiceType == 1).ExecuteCommand() != matchingIds.Length
                    || !isCurrent()) return false;
                db.Ado.CommitTran();
                committed = true;
                return true;
            }
            finally
            {
                if (!committed) db.Ado.RollbackTran();
            }
        }
    }
}
