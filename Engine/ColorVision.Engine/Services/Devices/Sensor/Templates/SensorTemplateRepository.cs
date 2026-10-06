using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Sensor.Local;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates;

internal static class SensorTemplateRepository
{
    internal static IReadOnlyList<LocalSensorCommand> ReadCommands(string category, int templateId, string? templateName)
    {
        if (!MySqlSetting.IsConnect) throw new InvalidOperationException("传感器模板需要连接 MySQL。");
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = DbType.MySql, IsAutoCloseConnection = true });
        var dictionary = db.Queryable<SysDictionaryModModel>().Where(value => value.Code == category && value.ModType == 5).Single()
            ?? throw new InvalidDataException($"找不到传感器模板类别：{category}");
        var query = db.Queryable<ModMasterModel>().Where(value => value.Pid == dictionary.Id && !value.IsDelete && value.TenantId == 0);
        var template = templateId > 0 ? query.Where(value => value.Id == templateId).Single() : query.Where(value => value.Name == templateName).Single();
        if (template == null) throw new InvalidDataException($"找不到传感器模板：{templateName}");
        var details = db.Queryable<ModDetailModel>().Where(value => value.Pid == template.Id).OrderBy(value => value.Id).ToList();
        return details.Where(value => value.IsEnable && !value.IsDelete).Select((value, index) =>
        {
            LocalSensorCommand command = LocalSensorCommand.FromTemplate(value.ValueA);
            command.Name = "Command " + (index + 1);
            return command;
        }).ToArray();
    }
}
