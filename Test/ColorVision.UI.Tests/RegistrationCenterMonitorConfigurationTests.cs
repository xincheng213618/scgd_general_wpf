using Microsoft.Data.Sqlite;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

// Protects the retired-service migration boundary: DB values survive restart,
// file/DB failures cannot report success, and other monitored services remain intact.
public sealed class RegistrationCenterMonitorConfigurationTests
{
    private static readonly Type Configuration = typeof(ServiceManagerViewModel).Assembly.GetType("WindowsServicePlugin.ServiceManager.RegistrationCenterMonitorConfiguration", throwOnError: true)!;

    private static T Invoke<T>(string method, params object[] arguments)
    {
        try
        {
            return (T)Configuration.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    [Theory]
    [InlineData("MySQL,CVArchService,CVMainService_x64,CustomService", "MySQL,CVMainService_x64,CustomService")]
    [InlineData(" cvarchservice ,MySQL,CVArchService,CVArchServiceBackup", "MySQL,CVArchServiceBackup")]
    [InlineData("CVArchService", "")]
    [InlineData("MySQL, CustomService ", "MySQL, CustomService ")]
    [InlineData("", "")]
    public void RemovalMatchesWholeServiceNamesAndIsIdempotent(string original, string expected)
    {
        string actual = Invoke<string>("RemoveArchiveService", original);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, Invoke<string>("RemoveArchiveService", actual));
    }

    [Theory]
    [InlineData("<add key=\"Monitor.Services\" value=\"CVArchService\" />")]
    [InlineData("")]
    public void CleanupUsesCurrentRegistrationCenterDatabaseListAndPreservesOtherSettings(string monitorSetting)
    {
        using SqliteConnection connection = CreateDatabase();
        string path = Path.GetTempFileName();
        File.WriteAllText(path, $"<configuration><appSettings><add key=\"NodeName\" value=\"RC_test\" />{monitorSetting}<add key=\"Monitor.CronExpression\" value=\"0/5 * * * * ? *\" /></appSettings></configuration>");
        try
        {
            for (int run = 0; run < 2; run++)
            {
                string services = Invoke<string>("RemoveArchiveServiceMonitoring", connection, "RC_test", path);
                Assert.Equal("MySQL,CustomService,CVMainService_x64", services);
                Assert.Equal(services, ReadServices(connection, 1));
                Assert.Equal("CVArchService,OtherMachine", ReadServices(connection, 2));
                XDocument document = XDocument.Load(path);
                XElement[] settings = document.Root!.Element("appSettings")!.Elements("add").ToArray();
                Assert.Equal(services, settings.Single(element => (string?)element.Attribute("key") == "Monitor.Services").Attribute("value")!.Value);
                Assert.Equal("0/5 * * * * ? *", settings.Single(element => (string?)element.Attribute("key") == "Monitor.CronExpression").Attribute("value")!.Value);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("RC_missing", "<configuration><appSettings /></configuration>")]
    [InlineData("RC_test", "<configuration />")]
    [InlineData("RC_test", "<configuration><appSettings><add key=\"NodeName\" value=\"RC_other\" /></appSettings></configuration>")]
    public void InvalidTargetOrConfigLeavesDatabaseAndFileUnchanged(string rcCode, string xml)
    {
        using SqliteConnection connection = CreateDatabase();
        string path = Path.GetTempFileName();
        File.WriteAllText(path, xml);
        byte[] original = File.ReadAllBytes(path);
        try
        {
            Assert.Throws<InvalidDataException>(() => Invoke<string>("RemoveArchiveServiceMonitoring", connection, rcCode, path));
            Assert.Equal("MySQL,CVArchService,CustomService,CVMainService_x64", ReadServices(connection, 1));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AmbiguousRegistrationCenterDoesNotUpdateEitherMonitor()
    {
        using SqliteConnection connection = CreateDatabase();
        Execute(connection, "INSERT INTO t_scgd_sys_config_rc VALUES ('RC_test', 2);");
        using DbTransaction transaction = connection.BeginTransaction();
        Assert.Throws<InvalidDataException>(() => Invoke<string>("UpdateDatabaseServices", connection, transaction, "RC_test", (Func<string, string>)(_ => "MySQL")));
        transaction.Rollback();
        Assert.Equal("MySQL,CVArchService,CustomService,CVMainService_x64", ReadServices(connection, 1));
        Assert.Equal("CVArchService,OtherMachine", ReadServices(connection, 2));
    }

    [Fact]
    public void LockedConfigFileRollsBackDatabaseChange()
    {
        using SqliteConnection connection = CreateDatabase();
        string path = Path.GetTempFileName();
        const string xml = "<configuration><appSettings><add key=\"Monitor.Services\" value=\"CVArchService\" /></appSettings></configuration>";
        File.WriteAllText(path, xml);
        try
        {
            using (FileStream fileLock = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() => Invoke<string>("RemoveArchiveServiceMonitoring", connection, "RC_test", path));
            }
            Assert.Equal("MySQL,CVArchService,CustomService,CVMainService_x64", ReadServices(connection, 1));
            Assert.Equal(xml, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CommitFailureRestoresExactOriginalFile()
    {
        string path = Path.GetTempFileName();
        byte[] original = System.Text.Encoding.UTF8.GetBytes("<configuration><appSettings><!--keep--><add key=\"Monitor.Services\" value=\"CVArchService\" /></appSettings></configuration>");
        File.WriteAllBytes(path, original);
        XDocument replacement = XDocument.Parse("<configuration><appSettings><add key=\"Monitor.Services\" value=\"MySQL\" /></appSettings></configuration>");
        bool observedNewConfig = false;
        try
        {
            Action commit = () =>
            {
                observedNewConfig = File.ReadAllText(path).Contains("MySQL", StringComparison.Ordinal);
                throw new IOException("database commit failed");
            };
            Assert.Throws<IOException>(() => Invoke<object>("SaveFileAndCommit", path, original, replacement, commit));
            Assert.True(observedNewConfig);
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(true, true, false, true, "stop,save,start", "")]
    [InlineData(false, true, false, true, "save", "")]
    [InlineData(false, true, true, true, "save", "save failed")]
    [InlineData(true, false, false, true, "stop", "未修改监控配置")]
    [InlineData(true, true, true, true, "stop,save,start", "save failed")]
    [InlineData(true, true, false, false, "stop,save,start", "配置已保存")]
    [InlineData(true, true, true, false, "stop,save,start", "更新失败")]
    public async Task ConfigurationChangePausesAndRestoresOnlyARunningRegistrationCenter(bool running, bool canStop, bool saveFails, bool canStart, string expectedCalls, string expectedError)
    {
        List<string> calls = [];
        Func<Task<bool>> stop = () => { calls.Add("stop"); return Task.FromResult(canStop); };
        Action save = () => { calls.Add("save"); if (saveFails) throw new IOException("save failed"); };
        Func<Task<bool>> start = () => { calls.Add("start"); return Task.FromResult(canStart); };
        var task = (Task)typeof(ServiceManagerViewModel).GetMethod("ApplyMonitoringChangeAsync", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [running, stop, save, start])!;
        Exception? error = await Record.ExceptionAsync(() => task);
        Assert.Equal(expectedCalls, string.Join(",", calls));
        if (expectedError.Length == 0)
            Assert.Null(error);
        else
            Assert.Contains(expectedError, Assert.IsAssignableFrom<Exception>(error).Message, StringComparison.Ordinal);
    }

    private static SqliteConnection CreateDatabase()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        Execute(connection, """
            CREATE TABLE t_scgd_sys_config_monitor(id INTEGER PRIMARY KEY, services TEXT);
            CREATE TABLE t_scgd_sys_config_rc(code TEXT, monitor_id INTEGER);
            INSERT INTO t_scgd_sys_config_monitor VALUES (1, 'MySQL,CVArchService,CustomService,CVMainService_x64'), (2, 'CVArchService,OtherMachine');
            INSERT INTO t_scgd_sys_config_rc VALUES ('RC_test', 1), ('RC_other', 2);
            """);
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string ReadServices(SqliteConnection connection, int id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT services FROM t_scgd_sys_config_monitor WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return (string)command.ExecuteScalar()!;
    }
}
