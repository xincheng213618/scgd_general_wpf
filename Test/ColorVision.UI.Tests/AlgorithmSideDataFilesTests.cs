using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.Ghost2;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Microsoft.VisualBasic.FileIO;
using SqlSugar;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmSideDataFilesTests
{
    [Fact]
    public void BoundGhostExportRetriesItsOriginalDatabaseWhenThatSourceComesBack() => StaTest.Run(() =>
    {
        using var files = new ExportDirectory();
        string source = Path.Combine(files.RootDirectory, "original-ghost.json");
        const string json = "{\"Analysis\":{\"message\":\"original\"}}";
        File.WriteAllText(source, json);
        ConnectionConfig config = new()
        {
            DbType = DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
            ConnectionString = $"Data Source={Path.Combine(files.RootDirectory, "original.db")};Pooling=False",
        };
        const int id = 21;
        using (var db = new SqlSugarClient(config))
        {
            db.CodeFirst.InitTables<DetailCommonModel>();
            db.Insertable(new DetailCommonModel { PId = id, ResultJson = JsonSerializer.Serialize(new ResultFile { ResultFileName = source }) }).ExecuteCommand();
        }

        bool available = false;
        var endpoint = new MySqlConnectedEndpoint(_ => false, () => available
            ? new SqlSugarClient(config)
            : throw new IOException("Synthetic original database outage."));
        var result = new ViewResultAlg(new AlgResultMasterModel
        {
            Id = id, ImgFileType = ViewResultAlgType.Ghost, version = "2.0", DatabaseEndpoint = endpoint,
        }, "original-batch");

        Assert.False(AlgorithmResultDataSaver.TrySave(new ViewResultContext(), result, files.OutputDirectory));
        Assert.Null(result.ViewResults);
        Assert.False(Directory.Exists(files.OutputDirectory));

        available = true;
        Assert.True(AlgorithmResultDataSaver.TrySave(new ViewResultContext(), result, files.OutputDirectory));
        Assert.Equal(json, File.ReadAllText(Assert.Single(Directory.GetFiles(files.OutputDirectory))));
    });

    [Fact]
    public void GhostExportsKeepOneUnmodifiedJsonDocumentPerResult()
    {
        using var files = new ExportDirectory();
        const string firstJson = "{\r\n  \"Analysis\": {\"message\": \"First\"}\r\n}";
        const string secondJson = "{\"Analysis\": {\"message\": \"Second\"}}";
        ViewResultAlg first = CreateGhost(17, firstJson);
        ViewResultAlg second = CreateGhost(23, secondJson);

        Assert.Empty(AlgorithmResultDataSaver.SaveSelected(new ViewResultContext(), [first, second], files.OutputDirectory));

        AssertDocument(files.Output("Batch-AGhost.json"), firstJson, "First");
        AssertDocument(files.Output("Batch-AGhost_result-Ghost-23.json"), secondJson, "Second");
        Assert.IsType<GhostView>(Assert.Single(second.ViewResults)).Result = firstJson + secondJson;
        Assert.False(AlgorithmResultDataSaver.TrySave(new ViewResultContext(), second, files.OutputDirectory));
        Assert.Equal(2, Directory.GetFiles(files.OutputDirectory).Length);

        ViewResultAlg CreateGhost(int id, string json)
        {
            string path = Path.Combine(files.RootDirectory, $"ghost-{id}.json");
            File.WriteAllText(path, json, Encoding.UTF8);
            return new ViewResultAlg
            {
                Id = id, Batch = "Batch-A", ResultType = ViewResultAlgType.Ghost, Version = "2.0",
                ViewResults = [new GhostView(new DetailCommonModel { ResultJson = JsonSerializer.Serialize(new ResultFile { ResultFileName = path }) })],
            };
        }

        static void AssertDocument(string path, string original, string message)
        {
            Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.Preamble));
            string saved = File.ReadAllText(path);
            Assert.Equal(original, saved);
            using var parsed = JsonDocument.Parse(saved);
            Assert.Equal(message, parsed.RootElement.GetProperty("Analysis").GetProperty("message").GetString());
        }
    }

    [Fact]
    public void PoiAnalysisAppendPreservesBothFieldsAndWritesOneHeader()
    {
        using var files = new ExportDirectory();
        const string content = "Center,\"left\"\r\nrecheck";
        var context = new ViewResultContext();
        ViewResultAlg MakeResult(int id, string text, double value) => new()
        {
            Id = id, Batch = "Batch-A", ResultType = ViewResultAlgType.PoiAnalysis, Version = "1.0",
            ViewResults = [new PoiAnalysisDetailViewReslut { PoiAnalysisResult = new() { result = new() { Content = text, Value = value } } }],
        };

        Assert.Empty(AlgorithmResultDataSaver.SaveSelected(context, [MakeResult(17, content, 1.25), MakeResult(23, "Right", 0)], files.OutputDirectory));

        string path = files.Output("PoiAnalysis_Batch-A.csv");
        Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.Preamble));
        using var csv = OpenCsv(path);
        Assert.Equal(["Content", "Value"], csv.ReadFields());
        Assert.Equal([content, 1.25.ToString(CultureInfo.CurrentCulture)], csv.ReadFields());
        Assert.Equal(["Right", "0"], csv.ReadFields());
        Assert.True(csv.EndOfData);
    }

    [Fact]
    public void MtfExportsRetainFlatAndGroupedLegacyColumnsWithoutReplacingSameBatch()
    {
        using var files = new ExportDirectory();
        const string label = "center,\"left\"";
        ViewResultAlg MakeResult(int id, MTFResult data) => new()
        {
            Id = id, Batch = "Batch-A", ResultType = ViewResultAlgType.MTF, Version = "2.0",
            ViewResults = [new MTFDetailViewReslut { MTFResult = data }],
        };
        var first = MakeResult(17, new() { result = [new() { name = label, x = 1, y = 2, w = 3, h = 4, mtfValue = 0 }] });
        var second = MakeResult(23, new()
        {
            resultChild = [new() { name = label, childRects = [new() { x = 5, y = 6, w = 7, h = 8, mtfValue = 0, id = 9 }], Average = 1 }],
        });

        string[] headers = ["name", "x", "y", "w", "h", "mtfValue"];
        Assert.Empty(AlgorithmResultDataSaver.SaveSelected(new ViewResultContext(), [first, second], files.OutputDirectory));
        using var flat = OpenCsv(files.Output("Batch-AMTF.csv"));
        Assert.Equal(headers, flat.ReadFields());
        Assert.Equal([label, "1", "2", "3", "4", "0"], flat.ReadFields());
        Assert.True(flat.EndOfData);
        using var grouped = OpenCsv(files.Output("Batch-AMTF_result-MTF-23.csv"));
        Assert.Equal(headers, grouped.ReadFields());
        Assert.Equal([label, "5", "6", "7", "8", "0", "9"], grouped.ReadFields());
        Assert.Equal([label, "", "", "", "HorizontalAverage", "0"], grouped.ReadFields());
        Assert.Equal([label, "", "", "", "verticalAverage", "0"], grouped.ReadFields());
        Assert.Equal([label, "", "", "", "Average", "1"], grouped.ReadFields());
        Assert.True(grouped.EndOfData);
    }

    [Theory]
    [InlineData(ViewResultAlgType.POI_Y_V2)]
    [InlineData(ViewResultAlgType.KB)]
    public void RawSideDataKeepsNameAndJsonInTwoOriginalCells(ViewResultAlgType type)
    {
        using var files = new ExportDirectory();
        string annotation = Path.Combine(files.RootDirectory, "annotation.png");
        File.WriteAllBytes(annotation, [10, 20, 30]);
        const string name = "Shift,\"left\"\r\nrow";
        const string value = "[{\"Y\":0,\"PixNumber\":4},\r\nnull]";
        ViewResultAlg result = Result(type, 17, name, annotation);
        Assert.IsType<PoiPointResultModel>(Assert.Single(result.ViewResults)).Value = value;
        result.ViewResults.Add(new PoiPointResultModel { PoiName = "one\ntwo", Value = "three\rfour" });

        AlgorithmResultDataSaver.Save(new ViewResultContext(), result, files.OutputDirectory);

        string path = files.Output("Batch-A.csv");
        Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.Preamble));
        string text = File.ReadAllText(path, Encoding.UTF8);
        Assert.StartsWith($"PoiName,Value{Environment.NewLine}", text);
        Assert.EndsWith(Environment.NewLine, text);
        using var csv = OpenCsv(path);
        Assert.Equal(["PoiName", "Value"], csv.ReadFields());
        Assert.Equal([name, value], csv.ReadFields());
        Assert.Equal(["one\ntwo", "three\rfour"], csv.ReadFields());
        Assert.True(csv.EndOfData);
    }

    [Fact]
    public void KeyboardCieSideDataKeepsItsHeaderOrderAndCultureFormattedValues()
    {
        using var files = new ExportDirectory();
        ViewResultAlg result = Result(ViewResultAlgType.KB_Output_CIE, 17, "Shift,\"left\"");
        Assert.IsType<PoiPointResultModel>(Assert.Single(result.ViewResults)).Value = """
            {"KeyLv":1.25,"HaloLv":7,
             "KeyCIE":{"U":0.1,"V":0.2,"X":0.3,"Y":0.4,"CCT":6500,"Wave":560},
             "HaloCIE":{"U":0.5,"V":0.6,"X":0.7,"Y":0.8,"CCT":6200,"Wave":580}}
            """;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            AlgorithmResultDataSaver.Save(new ViewResultContext(), result, files.OutputDirectory);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        using var csv = OpenCsv(files.Output("Batch-A.csv"));
        Assert.Equal(
            ["PoiName", "KeyLv", "HaloLv", "KeyCIE_U", "KeyCIE_V", "KeyCIE_X", "KeyCIE_Y", "KeyCIE_CCT", "KeyCIE_Wave",
             "HaloCIE_U", "HaloCIE_V", "HaloCIE_X", "HaloCIE_Y", "HaloCIE_CCT", "HaloCIE_Wave"],
            csv.ReadFields());
        Assert.Equal(
            ["Shift,\"left\"", "1,25", "7", "0,1", "0,2", "0,3", "0,4", "6500", "560", "0,5", "0,6", "0,7", "0,8", "6200", "580"],
            csv.ReadFields());
        Assert.True(csv.EndOfData);
    }

    [Theory]
    [InlineData(ViewResultAlgType.POI_Y, "POI_Y_Batch-A")]
    [InlineData(ViewResultAlgType.POI_XYZ, "POI_XYZ_Batch-A")]
    [InlineData(ViewResultAlgType.POI_Y_V2, "Batch-A")]
    public void PoiHandlersPreserveLegacyOutputAndAllocateResultRevisions(ViewResultAlgType type, string legacyStem)
    {
        using var files = new ExportDirectory();
        IResultHandleBase handler = type switch
        {
            ViewResultAlgType.POI_Y => new ViewHanlePOIY(),
            ViewResultAlgType.POI_XYZ => new ViewHanlePOIXZY(),
            _ => new ViewHandleRealPOI(),
        };
        string legacyPath = files.Output($"{legacyStem}.csv");
        handler.SideSave(Result(type, 17, "First"), files.OutputDirectory);
        byte[] original = File.ReadAllBytes(legacyPath);

        handler.SideSave(Result(type, 23, "Second"), files.OutputDirectory);
        handler.SideSave(Result(type, 23, "Second revision"), files.OutputDirectory);

        Assert.Equal(original, File.ReadAllBytes(legacyPath));
        Assert.Contains("First", File.ReadAllText(legacyPath));
        Assert.Contains("Second", File.ReadAllText(files.Output($"{legacyStem}_result-{type}-23.csv")));
        Assert.Contains("Second revision", File.ReadAllText(files.Output($"{legacyStem}_result-{type}-23-2.csv")));
        Assert.Equal(3, Directory.GetFiles(files.OutputDirectory).Length);
    }

    [Fact]
    public void KeyboardHandlerUsesTheSameRevisionForCsvAndImageAfterStalePartial()
    {
        using var files = new ExportDirectory();
        var handler = new ViewHandleKB();
        string annotation = Path.Combine(files.RootDirectory, "annotation.png");
        File.WriteAllBytes(annotation, [10, 20, 30]);
        handler.SideSave(Result(ViewResultAlgType.KB, 17, "First", annotation), files.OutputDirectory);
        string legacyCsv = files.Output("Batch-A.csv");
        byte[] original = File.ReadAllBytes(legacyCsv);

        string stale = files.Output("annotation_result-KB_Raw-23.png");
        File.WriteAllBytes(stale, [99]);
        File.WriteAllBytes(annotation, [40, 50, 60]);
        handler.SideSave(Result(ViewResultAlgType.KB_Raw, 23, "Second", annotation), files.OutputDirectory);

        Assert.Equal(original, File.ReadAllBytes(legacyCsv));
        Assert.Equal(new byte[] { 10, 20, 30 }, File.ReadAllBytes(files.Output("annotation.png")));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(stale));
        Assert.Equal(new byte[] { 40, 50, 60 }, File.ReadAllBytes(files.Output("annotation_result-KB_Raw-23-2.png")));
        Assert.Contains("Second", File.ReadAllText(files.Output("Batch-A_result-KB_Raw-23-2.csv")));
        Assert.Equal(5, Directory.GetFiles(files.OutputDirectory).Length);
    }

    [Fact]
    public async Task ConcurrentPoiExportsDoNotReplaceEachOthersMeasurements()
    {
        using var files = new ExportDirectory();
        string[] markers = ["First", "Second", "Third", "Fourth"];
        Task[] saves = markers.Select((marker, index) => Task.Run(() => new ViewHandleRealPOI()
            .SideSave(Result(ViewResultAlgType.RealPOI, index + 1, marker), files.OutputDirectory))).ToArray();
        await Task.WhenAll(saves);

        string[] outputs = Directory.GetFiles(files.OutputDirectory);
        Assert.Equal(markers.Length, outputs.Length);
        Assert.Contains(files.Output("Batch-A.csv"), outputs);
        string[] contents = outputs.Select(File.ReadAllText).ToArray();
        foreach (string marker in markers)
            Assert.Single(contents, content => content.Contains(marker, StringComparison.Ordinal));
    }

    [Fact]
    public void FailedStagedWriteLeavesTheExistingExportAndNoTemporaryFiles()
    {
        using var files = new ExportDirectory();
        Directory.CreateDirectory(files.OutputDirectory);
        string existing = files.Output("Batch-A.csv");
        File.WriteAllText(existing, "previous export");

        Assert.Throws<InvalidDataException>(() =>
            AlgorithmSideDataFiles.Write(Result(ViewResultAlgType.RealPOI, 17, "Failed"), files.OutputDirectory, "Batch-A.csv", stagingPath =>
            {
                File.WriteAllText(stagingPath, "incomplete");
                throw new InvalidDataException("simulated export interruption");
            }));

        Assert.Equal("previous export", File.ReadAllText(existing));
        Assert.Equal(existing, Assert.Single(Directory.EnumerateFiles(files.OutputDirectory)));
    }

    [Fact]
    public void KeyboardHandlerRollsBackItsNewImageWhenTheCsvCannotBePromoted()
    {
        using var files = new ExportDirectory();
        string annotation = Path.Combine(files.RootDirectory, "annotation.png");
        File.WriteAllBytes(annotation, [10, 20, 30]);
        ViewResultAlg result = Result(ViewResultAlgType.KB, 17, "Failed", annotation);
        result.Batch = new string('A', 280);

        Assert.ThrowsAny<IOException>(() => new ViewHandleKB().SideSave(result, files.OutputDirectory));

        Assert.Empty(Directory.EnumerateFiles(files.OutputDirectory));
        Assert.Equal(new byte[] { 10, 20, 30 }, File.ReadAllBytes(annotation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyboardHandlerReportsMissingAnnotationWithoutPublishingPartialCsv(bool hasPath)
    {
        using var files = new ExportDirectory();
        string? annotation = hasPath ? Path.Combine(files.RootDirectory, "missing.png") : null;

        Assert.Throws<FileNotFoundException>(() => new ViewHandleKB()
            .SideSave(Result(ViewResultAlgType.KB, 17, "Failed", annotation), files.OutputDirectory));

        if (Directory.Exists(files.OutputDirectory))
            Assert.Empty(Directory.EnumerateFiles(files.OutputDirectory));
    }

    private static TextFieldParser OpenCsv(string path)
    {
        var csv = new TextFieldParser(path, Encoding.UTF8) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        csv.SetDelimiters(",");
        return csv;
    }

    private static ViewResultAlg Result(ViewResultAlgType type, int id, string marker, string? annotation = null)
    {
        IViewResult detail = type switch
        {
            ViewResultAlgType.POI_Y => new PoiResultCIEYData { Id = 1, Name = marker, Y = 5 },
            ViewResultAlgType.POI_XYZ => new PoiResultCIExyuvData { Id = 1, Name = marker, Y = 5 },
            _ => new PoiPointResultModel { PoiName = marker, Value = "5" },
        };
        return new ViewResultAlg
        {
            Id = id,
            ResultType = type,
            Batch = "Batch-A",
            ResultImagFile = annotation ?? string.Empty,
            ViewResults = [detail],
        };
    }

    private sealed class ExportDirectory : IDisposable
    {
        internal string RootDirectory { get; } = Directory.CreateTempSubdirectory("ColorVision-AlgorithmSideData-").FullName;
        internal string OutputDirectory => Path.Combine(RootDirectory, "exports");
        internal string Output(string fileName) => Path.Combine(OutputDirectory, fileName);

        public void Dispose()
        {
            if (Directory.Exists(OutputDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(OutputDirectory))
                    File.Delete(file);
                Directory.Delete(OutputDirectory);
            }
            foreach (string file in Directory.EnumerateFiles(RootDirectory))
                File.Delete(file);
            Directory.Delete(RootDirectory);
        }
    }
}
