using System;
using System.Diagnostics;
using System.Text;

namespace ColorVision.Database
{
    /// <summary>
    /// MySQL 连接、命令行和 SQL 文件共同使用的字符集约定。
    /// </summary>
    public static class MySqlProtocolDefaults
    {
        public const string CharacterSet = "utf8mb4";
        public const string SetNamesStatement = "SET NAMES utf8mb4;";
        public const string DefaultCharacterSetArgument = "--default-character-set=utf8mb4";

        public static Encoding ScriptEncoding { get; } = new UTF8Encoding(false);

        public static void AddCharacterSetArgument(ProcessStartInfo startInfo)
        {
            ArgumentNullException.ThrowIfNull(startInfo);
            // 旧版 dump 会在结尾恢复启动时的客户端字符集，导入进程必须从 utf8mb4 启动。
            startInfo.ArgumentList.Add(DefaultCharacterSetArgument);
        }

        public static string CreateScript(params string?[] sections)
        {
            StringBuilder sql = new();
            sql.AppendLine(SetNamesStatement);
            foreach (string? section in sections)
            {
                if (!string.IsNullOrWhiteSpace(section))
                    sql.AppendLine(section.TrimEnd());
            }
            return sql.ToString();
        }
    }
}
