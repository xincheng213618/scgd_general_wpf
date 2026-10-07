using System.Globalization;
using System.IO;
using System.IO.Compression;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.ServiceManager;

public static class MySqlRuntimePrerequisite
{
    public static bool IsVc2013Installed()
    {
        return File.Exists(Path.Combine(Environment.SystemDirectory, "msvcr120.dll"))
            && File.Exists(Path.Combine(Environment.SystemDirectory, "msvcp120.dll"));
    }

    public static bool RequiresVc2013(Version version)
    {
        // MySQL 5.7.38/39 require both the 2013 and 2019 runtimes; 5.7.40+ and 8.x do not require 2013.
        return version.Major == 5 && version.Minor == 7 && version.Build < 40;
    }

    public static string? GetValidationMessage(Version? version, bool vc2013Installed)
    {
        if (version == null)
            return Resources.MySqlVersionUnreadable;

        return RequiresVc2013(version) && !vc2013Installed
            ? string.Format(CultureInfo.CurrentCulture, Resources.MySqlVc2013Required, version)
            : null;
    }

    public static Version? ReadVersion(string mysqlPath)
    {
        if (!string.Equals(Path.GetExtension(mysqlPath), ".zip", StringComparison.OrdinalIgnoreCase))
            return WinServiceHelper.GetFileVersion(mysqlPath);

        string temporaryExecutable = Path.Combine(Path.GetTempPath(), $"ColorVision-MySqlVersion-{Guid.NewGuid():N}.exe");
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(mysqlPath);
            ZipArchiveEntry? executable = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.Replace('\\', '/').EndsWith("/bin/mysqld.exe", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.Equals("bin/mysqld.exe", StringComparison.OrdinalIgnoreCase));
            if (executable == null)
                return null;

            using (Stream input = executable.Open())
            using (FileStream output = new(temporaryExecutable, FileMode.CreateNew))
                input.CopyTo(output);

            return WinServiceHelper.GetFileVersion(temporaryExecutable);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
        finally
        {
            try
            {
                File.Delete(temporaryExecutable);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log4net.LogManager.GetLogger(typeof(MySqlRuntimePrerequisite)).Warn("清理 MySQL 版本检测临时文件失败", ex);
            }
        }
    }
}
