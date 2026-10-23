using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.Templates;

public sealed record BuiltInTemplatePreset(TemplateCreatePreset Source, string ModuleGuide, string DeviceGuide, string Purpose, string PayloadJson);

/// <summary>Release-owned examples. Reading never creates or migrates a user database.</summary>
public static class BuiltInTemplateCatalog
{
    public static string DatabasePath => Path.Combine(AppContext.BaseDirectory, "Assets", "Templates", "BasicTemplates.db");

    public static IReadOnlyList<BuiltInTemplatePreset> Read(string templateCode, string? databasePath = null)
    {
        string path = databasePath ?? DatabasePath;
        if (!File.Exists(path)) throw new FileNotFoundException("内置基础模板库缺失，请修复软件安装。", path);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
            throw new InvalidDataException("内置基础模板库版本不兼容，请修复软件安装。");
        command.CommandText = """
            SELECT id, title, description, category, suggested_name, module_guide, device_guide, purpose, payload_json
            FROM template_preset WHERE template_code = $code ORDER BY sort_order, id;
            """;
        command.Parameters.AddWithValue("$code", templateCode);
        using var reader = command.ExecuteReader();
        var presets = new List<BuiltInTemplatePreset>();
        while (reader.Read())
            presets.Add(new(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), "内置 · " + reader.GetString(3), reader.GetString(4))
                { Guide = new(reader.GetString(7), reader.GetString(5), reader.GetString(6)) },
                reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8)));
        return presets.AsReadOnly();
    }
}
