using ColorVision.Database;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Types;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ColorVision.Engine.Services.Terminal;

internal static class TerminalDeviceCreation
{
    internal static bool TryPersist(TerminalService terminal, TerminalConfigurationTarget target, IDeviceServiceFactory factory,
        string code, string name, [NotNullWhen(true)] out SysResourceModel? resource)
    {
        resource = null;
        if (string.IsNullOrWhiteSpace(code) || !target.IsCurrent(terminal)) return false;
        SysResourceModel parent = target.ResourceSnapshot;
        var serviceType = (ServiceTypes)parent.Type;
        if (factory.ServiceType != serviceType) return false;

        var terminalConfig = terminal.Config;
        string sendTopic = terminalConfig.SendTopic;
        string subscribeTopic = terminalConfig.SubscribeTopic;
        var context = new DeviceServiceCreateContext(code, name, sendTopic, subscribeTopic);
        DeviceServiceConfig config = factory.CreateConfig(context);
        if (!IsCurrent()) return false;

        var child = new SysResourceModel { Code = code, Name = name, Type = parent.Type, Pid = parent.Id, TenantId = parent.TenantId };
        bool needsLicense = serviceType is not (ServiceTypes.Camera or ServiceTypes.Calibration or ServiceTypes.Spectrum)
            && string.IsNullOrWhiteSpace(config.SN);
        int id;
        if (parent.Id > 0 && target.Endpoint is { } endpoint)
        {
            id = TryInsertRemote(endpoint, target, child, needsLicense, SetConfiguration, IsCurrent);
        }
        else if (SysResourceDao.IsLocalId(parent.Id) && target.LocalResources is { } local)
        {
            SetConfiguration(needsLicense ? new PhyLicenseDao(local.LocalResourceStore).GetLocal() : []);
            id = local.TryInsertChildResource(parent, child, IsCurrent);
        }
        else return false;

        if (id == 0) return false;
        child.Id = id;
        resource = child;
        return true;

        bool IsCurrent() => target.IsCurrent(terminal)
            && string.Equals(terminalConfig.SendTopic, sendTopic, StringComparison.Ordinal)
            && string.Equals(terminalConfig.SubscribeTopic, subscribeTopic, StringComparison.Ordinal);

        void SetConfiguration(IReadOnlyList<LicenseModel> licenses)
        {
            DeviceServiceFactoryRegistry.ApplyDefaultLicense(serviceType, config, licenses, DateTimeOffset.Now);
            child.Value = JsonConvert.SerializeObject(config);
        }
    }

    private static int TryInsertRemote(MySqlConnectedEndpoint endpoint, TerminalConfigurationTarget target, SysResourceModel child,
        bool needsLicense, Action<IReadOnlyList<LicenseModel>> prepare, Func<bool> isCurrent)
    {
        if (target.ResourceId <= 0 || !isCurrent()) return 0;
        using var db = endpoint.CreateDbClient();
        if (!isCurrent()) return 0;

        bool mysql = db.CurrentConnectionConfig.DbType == DbType.MySql;
        if (mysql) db.Ado.BeginTran(System.Data.IsolationLevel.RepeatableRead);
        else db.Ado.BeginTran();

        bool committed = false;
        try
        {
            if (mysql && db.Ado.SqlQuery<int>("SELECT id FROM t_scgd_sys_resource WHERE id = @id FOR UPDATE",
                new SugarParameter("@id", target.ResourceId)).Count != 1) return 0;
            var parent = db.Queryable<SysResourceModel>().First(row => row.Id == target.ResourceId);
            if (parent == null || !target.Matches(parent) || !isCurrent()) return 0;

            // Lock the matching code range so concurrent creators cannot both commit the same code.
            string locking = mysql ? " FOR UPDATE" : string.Empty;
            var matches = db.Ado.SqlQuery<CompetingDevice>(
                "SELECT code AS Code, `type` AS Type FROM t_scgd_sys_resource WHERE code = @code AND tenant_id = @tenant "
                + "AND pid IS NOT NULL AND is_enable = 1 AND is_delete = 0" + locking,
                new SugarParameter("@code", child.Code), new SugarParameter("@tenant", child.TenantId));
            if (matches.Any(row => string.Equals(row.Code, child.Code, StringComparison.Ordinal)
                && DeviceServiceFactoryRegistry.TryGetFactory((ServiceTypes)row.Type, out _)) || !isCurrent()) return 0;

            prepare(needsLicense ? db.Queryable<LicenseModel>().ToList() : []);
            if (!isCurrent()) return 0;
            int id = db.Insertable(child).ExecuteReturnIdentity();
            if (id <= 0 || !isCurrent()) return 0;

            db.Ado.CommitTran();
            committed = true;
            return id;
        }
        finally
        {
            if (!committed) db.Ado.RollbackTran();
        }
    }

    private sealed class CompetingDevice
    {
        public string? Code { get; set; }
        public int Type { get; set; }
    }
}
