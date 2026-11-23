using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI.Desktop.Feedback;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class FeedbackDatabaseCollectorTests
    {
        [Fact]
        public void DatabaseAndLicenseCollectorsAreSelectedByDefault()
        {
            var databaseCollector = new MySqlResourceFeedbackCollector();
            var licenseCollector = new LicenseFeedbackCollector();

            Assert.True(databaseCollector.IsSelectedByDefault);
            Assert.True(licenseCollector.IsSelectedByDefault);
            Assert.True(new CollectorItem(databaseCollector).IsChecked);
            Assert.True(new CollectorItem(licenseCollector).IsChecked);
            Assert.Equal("数据库资源与配置", databaseCollector.Name);
            Assert.Equal("许可证信息", licenseCollector.Name);
        }

        [Fact]
        public void CameraTimingExportUsesTheSelectedRangeAndPreservesMeasuredAndLegacyRows()
        {
            var collector = new CameraCaptureTimingFeedbackCollector();
            var item = new CollectorItem(collector);
            Assert.True(item.IsChecked);
            Assert.Equal(7, item.SelectedDays);
            item.SelectedDays = 1;
            Assert.Equal(1, collector.RecentDays);

            using var database = new SqliteConnection("Data Source=:memory:");
            database.Open();
            using var command = database.CreateCommand();
            command.CommandText = """
                CREATE TABLE t_scgd_measure_result_img (
                    id INTEGER PRIMARY KEY, batch_id INTEGER, create_date DATETIME, device_code TEXT,
                    raw_file TEXT, total_time INTEGER, params TEXT)
                """;
            command.ExecuteNonQuery();
            command.CommandText = """
                INSERT INTO t_scgd_measure_result_img VALUES
                ($id, 42, $created, 'SV6100_Camera', $raw, 735, $params)
                """;
            var to = new DateTimeOffset(2026, 11, 20, 5, 3, 37, TimeSpan.FromHours(8));
            DateTime from = to.DateTime.AddDays(-1);
            const string measured = """
                {"ExpTime":[101.1],"Calibration":{"Name":"White51"},"Timing":{"Capture":539,"Calibration":72,"Save":124,"Total":735,"Extra":3}}
                """;
            void Insert(int id, DateTime time, string? parameters)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$id", id);
                command.Parameters.AddWithValue("$created", time);
                command.Parameters.AddWithValue("$raw", $"shot-{id}.cvraw");
                command.Parameters.AddWithValue("$params", (object?)parameters ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
            Insert(1, from.AddSeconds(-1), measured);
            Insert(4, from, """{"ExpTime":101.1}""");
            Insert(2, from, measured);
            Insert(3, to.DateTime, null);
            Insert(5, to.DateTime.AddSeconds(1), measured);

            using var writer = new StringWriter();
            CameraCaptureTimingFeedbackCollector.WriteExport(database, writer, to, collector.RecentDays);

            JObject export = JObject.Parse(writer.ToString());
            Assert.Equal("t_scgd_measure_result_img", export.Value<string>("SourceTable"));
            Assert.Equal("ok", export.Value<string>("Status"));
            Assert.Equal(3, export.Value<int>("Count"));
            var rows = (JArray)export["Rows"]!;
            Assert.Equal(new[] { 2, 4, 3 }, rows.Select(row => row.Value<int>("Id")));
            var current = rows[0]!;
            Assert.Equal(42, current.Value<int>("BatchId"));
            Assert.Equal(from, current.Value<DateTime>("CreatedAt"));
            Assert.Equal("SV6100_Camera", current.Value<string>("DeviceCode"));
            Assert.Equal("shot-2.cvraw", current.Value<string>("RawFile"));
            Assert.Equal(735, current.Value<int>("TotalTimeMs"));
            Assert.Equal("White51", current.Value<string>("CalibrationTemplate"));
            Assert.Equal(101.1, Assert.IsType<JArray>(current["ExposureMs"])[0]!.Value<double>());
            Assert.True(JToken.DeepEquals(JObject.Parse("""{"Capture":539,"Calibration":72,"Save":124,"Total":735}"""), current["Timing"]));
            Assert.Equal(101.1, rows[1]!.Value<double>("ExposureMs"));
            Assert.Equal(JTokenType.Null, rows[1]!["Timing"]!.Type);
            Assert.Equal(JTokenType.Null, rows[2]!["Timing"]!.Type);
        }

        [Fact]
        public void LicenseCollectorCreatesImportableFilesAndReadableIndex()
        {
            var licenses = new[]
            {
                new LicenseModel
                {
                    Id = 7,
                    MacAddress = "CAM:01",
                    LicenseValue = "encoded-license-one",
                    Model = "CV-Camera",
                    CusTomerName = "ColorVision",
                    LiceType = 0,
                    DevCameraId = 21,
                    DevCaliId = 22,
                    ExpiryDate = new DateTime(2028, 6, 30),
                    CreateDate = new DateTime(2026, 8, 27),
                },
                new LicenseModel
                {
                    Id = 8,
                    MacAddress = "CAM:01",
                    LicenseValue = "encoded-license-two",
                    Model = "CV-Camera-2",
                    LiceType = 0,
                },
                new LicenseModel
                {
                    Id = 9,
                    MacAddress = "EMPTY",
                    LicenseValue = null,
                    Model = "Unassigned",
                    LiceType = 1,
                },
            };

            IReadOnlyList<(string EntryPath, string FilePath)> files = LicenseFeedbackCollector.CreateFiles(
                licenses,
                new DateTimeOffset(2026, 8, 27, 9, 30, 0, TimeSpan.FromHours(8)));

            try
            {
                Assert.Equal(3, files.Count);
                Assert.Equal("License/licenses.json", files[0].EntryPath);
                Assert.Contains(files, item => item.EntryPath == "License/CAM_01.lic" && File.ReadAllText(item.FilePath) == "encoded-license-one");
                Assert.Contains(files, item => item.EntryPath == "License/CAM_01-2.lic" && File.ReadAllText(item.FilePath) == "encoded-license-two");

                JObject index = JObject.Parse(File.ReadAllText(files[0].FilePath));
                Assert.Equal(3, index.Value<int>("Count"));
                Assert.Equal("t_scgd_camera_license", index.Value<string>("SourceTable"));
                Assert.Equal("CAM_01.lic", index["Licenses"]![0]!.Value<string>("LicenseFile"));
                Assert.False(index["Licenses"]![2]!.Value<bool>("HasLicenseValue"));
                Assert.Equal(JTokenType.Null, index["Licenses"]![2]!["LicenseFile"]!.Type);
            }
            finally
            {
                foreach ((_, string filePath) in files)
                {
                    if (File.Exists(filePath) && filePath.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                        File.Delete(filePath);
                }
            }
        }

        [Fact]
        public void ResourceFeedbackBackupContainsOnlyConfigurationTables()
        {
            IReadOnlyList<string> tables = MySqlLocalServicesManager.MigrationBackupTableNames;

            Assert.Equal(9, tables.Count);
            Assert.Contains("t_scgd_camera_license", tables);
            Assert.DoesNotContain(tables, table => table.StartsWith("t_scgd_algorithm_result", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(tables, table => table.StartsWith("t_scgd_measure_result", StringComparison.OrdinalIgnoreCase));
        }
    }
}
