using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Browser;

/// <summary>Local browser preferences, separate from template identities and server data.</summary>
internal sealed class TemplateBrowserOrderStore(string databasePath)
{
    // Keep the original table and Flow keys so existing preferences survive the shared browser.
    // Additional template kinds prefix their scopes (for example, poi:local).
    internal static string MySqlScope(string host, int port, string database) => "mysql:" + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { host.ToLowerInvariant(), port.ToString(), database }))));

    internal Task<string[]> LoadAsync(string scope) => Task.Run(() =>
    {
        if (!File.Exists(databasePath)) return Array.Empty<string>();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT template_keys FROM flow_template_browser_order WHERE source_key=$scope";
        command.Parameters.AddWithValue("$scope", scope);
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<string[]>(json) ?? [] : [];
    });

    internal Task SaveAsync(string scope, IReadOnlyList<string> keys) => Task.Run(() =>
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO flow_template_browser_order(source_key,template_keys) VALUES($scope,$keys) ON CONFLICT(source_key) DO UPDATE SET template_keys=$keys";
        command.Parameters.AddWithValue("$scope", scope);
        command.Parameters.AddWithValue("$keys", JsonSerializer.Serialize(keys));
        command.ExecuteNonQuery();
    });

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false, DefaultTimeout = 1 }.ToString());
        try
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS flow_template_browser_order(source_key TEXT PRIMARY KEY,template_keys TEXT NOT NULL)";
            command.ExecuteNonQuery();
            return db;
        }
        catch { db.Dispose(); throw; }
    }
}
