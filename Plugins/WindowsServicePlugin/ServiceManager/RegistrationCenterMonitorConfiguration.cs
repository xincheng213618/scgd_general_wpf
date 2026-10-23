using System.Data;
using System.Data.Common;
using System.IO;
using System.Xml.Linq;

namespace WindowsServicePlugin.ServiceManager;

internal static class RegistrationCenterMonitorConfiguration
{
    internal static string RemoveArchiveService(string services)
    {
        string[] entries = services.Split(',');
        return entries.Any(IsArchiveService)
            ? string.Join(",", entries.Where(entry => !IsArchiveService(entry)))
            : services;
    }

    private static bool IsArchiveService(string service) => service.Trim().Equals("CVArchService", StringComparison.OrdinalIgnoreCase);

    internal static void SetMonitorServices(XDocument document, string services)
    {
        XElement settings = document.Element("configuration")?.Element("appSettings")
            ?? throw new InvalidDataException("WinService.config 缺少 appSettings");
        XElement[] entries = settings.Elements("add").Where(element => (string?)element.Attribute("key") == "Monitor.Services").ToArray();
        if (entries.Length == 0)
            settings.Add(new XElement("add", new XAttribute("key", "Monitor.Services"), new XAttribute("value", services)));
        else
            foreach (XElement entry in entries)
                entry.SetAttributeValue("value", services);
    }

    internal static string UpdateDatabaseServices(DbConnection connection, DbTransaction transaction, string rcCode, Func<string, string> update)
    {
        if (string.IsNullOrWhiteSpace(rcCode))
            throw new InvalidDataException("注册中心配置缺少 RCName，无法同步监控服务列表");

        using DbCommand lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.CommandText = """
            SELECT DISTINCT monitor.id, monitor.services
            FROM t_scgd_sys_config_monitor AS monitor
            INNER JOIN t_scgd_sys_config_rc AS rc ON rc.monitor_id = monitor.id
            WHERE rc.code = @rcCode;
            """;
        AddParameter(lookup, "@rcCode", rcCode);
        long monitorId;
        string services;
        using (DbDataReader reader = lookup.ExecuteReader())
        {
            if (!reader.Read())
                throw new InvalidDataException($"数据库中未找到注册中心 {rcCode} 对应的监控配置");
            monitorId = Convert.ToInt64(reader.GetValue(0));
            services = update(reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
            if (reader.Read())
                throw new InvalidDataException($"注册中心 {rcCode} 对应多个监控配置，请先检查数据库");
        }

        using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE t_scgd_sys_config_monitor SET services = @services WHERE id = @monitorId;";
        AddParameter(command, "@services", services);
        AddParameter(command, "@monitorId", monitorId);
        command.ExecuteNonQuery();
        return services;
    }

    internal static string RemoveArchiveServiceMonitoring(DbConnection connection, string rcCode, string configPath)
    {
        // 注册中心启动时会用数据库覆盖本地列表；两处必须在恢复服务前写好。
        byte[] original = File.ReadAllBytes(configPath);
        XDocument document = XDocument.Load(configPath, LoadOptions.PreserveWhitespace);
        string? configuredName = document.Element("configuration")?.Element("appSettings")?.Elements("add")
            .FirstOrDefault(element => (string?)element.Attribute("key") == "NodeName")?.Attribute("value")?.Value;
        if (!string.IsNullOrWhiteSpace(configuredName) && !string.Equals(configuredName, rcCode, StringComparison.Ordinal))
            throw new InvalidDataException("本地注册中心 NodeName 与当前 RCName 不一致，请核对服务目录和数据库连接");

        using DbTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        string services = UpdateDatabaseServices(connection, transaction, rcCode, RemoveArchiveService);
        SetMonitorServices(document, services);
        SaveFileAndCommit(configPath, original, document, transaction.Commit);
        return services;
    }

    internal static void SaveFileAndCommit(string configPath, byte[] original, XDocument document, Action commit)
    {
        WriteAtomically(configPath, path => document.Save(path));
        try
        {
            commit();
        }
        catch (Exception commitError)
        {
            try
            {
                WriteAtomically(configPath, path => File.WriteAllBytes(path, original));
            }
            catch (Exception restoreError)
            {
                throw new AggregateException("数据库提交失败，本地监控配置恢复也失败，请核对两处配置", commitError, restoreError);
            }
            throw;
        }
    }

    private static void WriteAtomically(string configPath, Action<string> write)
    {
        string temporaryPath = configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            write(temporaryPath);
            File.Replace(temporaryPath, configPath, destinationBackupFileName: null);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
