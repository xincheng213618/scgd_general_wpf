using ColorVision.Database;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.PhyCameras.Licenses;

internal sealed class LicenseCatalogSource
{
    private readonly PhyLicenseDao? local;
    private readonly MySqlConnectedEndpoint? endpoint;

    internal LicenseCatalogSource(MySqlConnectedEndpoint endpoint) => this.endpoint = endpoint;

    private LicenseCatalogSource(PhyLicenseDao local) => this.local = local;

    internal static LicenseCatalogSource Capture()
    {
        PhyLicenseDao dao = PhyLicenseDao.Instance;
        if (dao.UseLocal) return new LicenseCatalogSource(dao);

        MySqlConnectedEndpoint? endpoint = MySqlControl.GetInstance().ConnectedEndpoint;
        if (endpoint is not { IsCurrent: true }) throw SourceChanged();
        return new LicenseCatalogSource(endpoint);
    }

    internal bool Matches(LicenseCatalogSource? other) => other != null
        && ReferenceEquals(local, other.local) && ReferenceEquals(endpoint, other.endpoint);

    internal void EnsureCurrent()
    {
        if (local != null ? !ReferenceEquals(local, PhyLicenseDao.Instance) || !local.UseLocal : endpoint?.IsCurrent != true)
            throw SourceChanged();
    }

    internal static InvalidOperationException SourceChanged() =>
        new(EngineLocalization.Get("许可证来源已变更，请刷新列表后重试。"));

    internal IReadOnlyList<LicenseModel> Load()
    {
        EnsureCurrent();
        using var db = endpoint?.CreateDbClient();
        var licenses = local != null ? local.GetLocal() : db!.Queryable<LicenseModel>().ToList();
        EnsureCurrent();
        return licenses;
    }

    internal bool Delete(int id)
    {
        EnsureCurrent();
        if (local != null) return local.TryDeleteLocal(id);

        if (id <= 0) return false;
        using var db = endpoint!.CreateDbClient();
        EnsureCurrent();
        return db.Deleteable<LicenseModel>().Where(item => item.Id == id).ExecuteCommand() > 0;
    }

    internal async Task<int> DeleteExpiredAsync(int[] ids, DateTime cutoff)
    {
        EnsureCurrent();
        if (local != null) return local.DeleteExpiredLocal(ids, cutoff);

        using var db = endpoint!.CreateDbClient();
        EnsureCurrent();
        return await PhyLicenseDao.DeleteExpiredAsync(db, ids, cutoff).ConfigureAwait(false);
    }
}
