using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI.Desktop.Feedback;
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
