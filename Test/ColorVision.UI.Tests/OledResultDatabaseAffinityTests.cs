using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using SqlSugar;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class OledResultDatabaseAffinityTests
{
    [Fact]
    public void TaggedOledResultUsesItsOriginalDetailsAndImageAfterAReconnect()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-oled-result-");
        try
        {
            WpfTestHost.Invoke(() =>
            {
                const int resultId = 17;
                string originalDbPath = Path.Combine(directory.FullName, "original.db");
                string replacementDbPath = Path.Combine(directory.FullName, "replacement.db");
                string originalImage = Path.Combine(directory.FullName, "original.tif");
                string replacementImage = Path.Combine(directory.FullName, "replacement.tif");
                Seed(originalDbPath, resultId, originalImage);
                Seed(replacementDbPath, resultId, replacementImage);

                MySqlConnectedEndpoint? active = null;
                bool originalAvailable = false;
                int replacementOpens = 0;
                var original = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, active), () =>
                    originalAvailable ? Open(originalDbPath) : throw new IOException("Original fixture database is unavailable."));
                var replacement = new MySqlConnectedEndpoint(candidate => ReferenceEquals(candidate, active), () =>
                {
                    replacementOpens++;
                    return Open(replacementDbPath);
                });
                active = original;
                var master = new AlgResultMasterModel
                {
                    Id = resultId, ImgFileType = ViewResultAlgType.OLED_FindDotsArrayMem, DatabaseEndpoint = original,
                };
                var result = new ViewResultAlg(master, "oled-fixture");
                var handler = new ResultOLED_RebuildPixelsMem();
                var context = new ViewResultContext();
                active = replacement;

                Assert.True(handler.CanHandle1(result));
                Assert.Throws<IOException>(() => handler.Load(context, result));
                Assert.Null(result.ViewResults);

                originalAvailable = true;
                handler.Load(context, result);

                var detail = Assert.IsType<AlgResultPoiCieFileModel>(Assert.Single(result.ViewResults));
                Assert.Equal(originalImage, detail.FileUrl);
                Assert.Equal(originalImage, result.FilePath);
                Assert.Equal(originalImage, master.ImgFile);
                Assert.Equal(0, replacementOpens);
            });
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static void Seed(string path, int resultId, string image)
    {
        using var db = Open(path);
        db.CodeFirst.InitTables<AlgResultPoiCieFileModel>();
        db.Insertable(new AlgResultPoiCieFileModel
        {
            Pid = resultId, FileName = Path.GetFileName(image), FileUrl = image, FileType = "tif",
        }).ExecuteCommand();
    }

    private static SqlSugarClient Open(string path) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite,
        InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true,
    });
}
