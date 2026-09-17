using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.OpticCenter;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class RgbCrossProcessTests
{
    private static JObject Measurement()
    {
        var points = new JArray();
        for (int i = 0; i < 9; i++)
        {
            double x = 100 + i % 3 * 100, y = 100 + i / 3 * 100;
            var channels = new JObject();
            foreach (var (name, shift) in new[] { ("R", 1d), ("G", 0d), ("B", -0.5d) })
                channels[name] = new JObject { ["status"] = "VALID", ["reasonCodes"] = new JArray(), ["horizontalArm"] = Box(x - 15, y - 2, 30, 4), ["verticalArm"] = Box(x - 2 + shift, y - 15, 4, 30) };
            points.Add(new JObject { ["id"] = $"P{i + 1}", ["row"] = i / 3 + 1, ["column"] = i % 3 + 1, ["status"] = "VALID", ["reasonCodes"] = new JArray(), ["warnings"] = new JArray(), ["region"] = Box(x - 20, y - 20, 40, 40), ["channels"] = channels, ["separation"] = new JObject { ["maximumEdgeSeparationPx"] = 1.5, ["leftEdgeSpreadPx"] = 1.5, ["rightEdgeSpreadPx"] = 1.5, ["topEdgeSpreadPx"] = 0, ["bottomEdgeSpreadPx"] = 0 } });
        }
        return new JObject { ["schemaId"] = "colorvision.rgb-cross-measurement", ["schemaVersion"] = "1.0.0", ["capabilityProfile"] = "rgb-cross.measurement.v1", ["measurementId"] = Guid.NewGuid().ToString(), ["algorithm"] = new JObject { ["id"] = RgbCrossResultParser.AlgorithmId, ["version"] = "1.2.0" }, ["source"] = new JObject { ["imageId"] = "synthetic", ["width"] = 400, ["height"] = 400, ["sha256"] = null }, ["coordinates"] = new JObject { ["space"] = "source-image", ["unit"] = "px", ["origin"] = "top-left", ["pixelCenters"] = "integer", ["axes"] = "x-right-y-down" }, ["execution"] = new JObject { ["status"] = "SUCCEEDED" }, ["searchRegion"] = Box(0, 0, 400, 400), ["summary"] = new JObject { ["validPointCount"] = 9, ["invalidPointCount"] = 0, ["complete"] = true, ["maximumEdgeSeparationPx"] = 1.5 }, ["points"] = points };
    }
    private static JObject Box(double x, double y, double w, double h) => new() { ["x"] = x, ["y"] = y, ["width"] = w, ["height"] = h };

    [Fact]
    public void MeasurementAndJudgmentAreSeparateAndZeroIsARealLimit()
    {
        var result = RgbCrossResultParser.Parse(Measurement().ToString());
        Assert.Equal("MEASURED", result.Status); Assert.Null(result.AppliedLimit);
        RgbCrossResultParser.Evaluate(result, 1.5); Assert.Equal("PASS", result.Status);
        RgbCrossResultParser.Evaluate(result, 1.4); Assert.Equal("FAIL", result.Status);
        RgbCrossResultParser.Evaluate(result, 0); Assert.Equal("FAIL", result.Status);
        RgbCrossResultParser.Evaluate(result, null); Assert.Equal("MEASURED", result.Status);
        Assert.All(result.Points, p => Assert.Equal(1.5, p.MaximumEdgeSeparation));
    }

    [Theory]
    [InlineData("version")][InlineData("duplicate")][InlineData("missing")][InlineData("nonfinite")][InlineData("mismatch")][InlineData("bounds")][InlineData("execution")][InlineData("summary")][InlineData("search")]
    public void InvalidPayloadCannotBecomeSuccessfulMeasurements(string defect)
    {
        var json = Measurement(); var rows = (JArray)json["points"]!;
        switch (defect)
        {
            case "version": json["schemaVersion"] = "9.0"; break;
            case "duplicate": rows[1]["id"] = "P1"; break;
            case "missing": rows.RemoveAt(8); break;
            case "nonfinite": rows[0]["channels"]!["R"]!["horizontalArm"]!["x"] = double.NaN; break;
            case "mismatch": rows[0]["separation"]!["maximumEdgeSeparationPx"] = 0; break;
            case "bounds": rows[0]["region"]!["width"] = 1000; break;
            case "execution": json["execution"]!["status"] = "FAILED"; break;
            case "summary": json["summary"]!["validPointCount"] = 8; break;
            case "search": json["searchRegion"]!["width"] = 20; break;
        }
        Assert.ThrowsAny<Exception>(() => RgbCrossResultParser.Parse(json.ToString()));
    }

    [Fact]
    public void InvalidPointKeepsValidChannelsButHasNoMeasurementOrPass()
    {
        var json = Measurement(); var p = json["points"]![5]!;
        json["summary"]!["validPointCount"] = 8; json["summary"]!["invalidPointCount"] = 1; json["summary"]!["complete"] = false;
        p["status"] = "INVALID"; p["reasonCodes"] = new JArray("B:ambiguous_arm_edges"); p["separation"] = null;
        p["channels"]!["B"] = new JObject { ["status"] = "INVALID", ["horizontalArm"] = null, ["verticalArm"] = null };
        var result = RgbCrossResultParser.Parse(json.ToString()); RgbCrossResultParser.Evaluate(result, 2);
        Assert.Equal("INVALID", result.Status); Assert.Null(result.Points[5].MaximumEdgeSeparation); Assert.Equal(2, result.Points[5].Channels.Count);
    }

    [Fact]
    public void ExistingArtifactExportIgnoresItsCustomerJudgment()
    {
        var source = Measurement(); var rows = new JArray(); var shapes = new JArray();
        foreach (var p in source["points"]!)
        {
            var r = p["region"]!;
            rows.Add(new JObject { ["point"] = p["id"], ["row"] = p["row"], ["column"] = p["column"], ["valid"] = true, ["result"] = "NG", ["maximumEdgeSeparation_px"] = 1.5, ["roiX_px"] = r["x"], ["roiY_px"] = r["y"], ["roiWidth_px"] = r["width"], ["roiHeight_px"] = r["height"] });
            foreach (string channel in new[] { "R", "G", "B" }) foreach (var (arm, key) in new[] { ("horizontal", "horizontalArm"), ("vertical", "verticalArm") })
            {
                var b = p["channels"]![channel]![key]!;
                shapes.Add(new JObject { ["id"] = $"{p["id"]}-{channel}-{arm}", ["kind"] = "rectangle", ["points"] = new JArray(new JObject { ["x"] = b["x"], ["y"] = b["y"] }, new JObject { ["x"] = b.Value<double>("x") + b.Value<double>("width"), ["y"] = b.Value<double>("y") + b.Value<double>("height") }) });
            }
        }
        var root = new JObject { ["algorithmId"] = RgbCrossResultParser.AlgorithmId, ["algorithmVersion"] = "1.2.0", ["invocationId"] = "test-run", ["status"] = "Succeeded", ["artifacts"] = new JArray(new JObject { ["kind"] = "table", ["name"] = "RGB-cross-separation", ["rows"] = rows }, new JObject { ["kind"] = "geometry", ["name"] = "rgb-cross-regions", ["coordinateSpace"] = "pixel", ["geometries"] = shapes }) };
        var parsed = RgbCrossResultParser.Parse(root.ToString()); Assert.Equal("MEASURED", parsed.Status); Assert.Equal(9, parsed.Points.Count);
    }

    [Fact]
    public async Task ExecutionAndCsvSnapshotMeasurementWithoutImplicitPass()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllTextAsync(path, Measurement().ToString());
            var ctx = new IProcessExecutionContext { Result = new() { Result = true }, ObjectiveTestResult = new() };
            var process = new RgbCrossProcess(); process.Config.ResultJsonFile = path;
            Assert.True(await process.Execute(ctx)); Assert.All(process.GetObjectiveCsvRows(ctx.Result), row => Assert.Equal("MEASURED", row.TestResult));
            process.Config.RecipeConfig.EnableJudgment = true; process.Config.RecipeConfig.MaximumEdgeSeparationPixels = 1;
            Assert.All(process.GetObjectiveCsvRows(ctx.Result), row => Assert.Equal("MEASURED", row.TestResult));
            Assert.True(await process.Execute(ctx)); Assert.False(ctx.Result.Result); Assert.All(process.GetObjectiveCsvRows(ctx.Result), row => Assert.Equal("FAIL", row.TestResult));
            process.Config.RecipeConfig.MaximumEdgeSeparationPixels = null;
            Assert.False(await process.Execute(ctx)); Assert.Equal("DATA_ERROR", Assert.Single(process.GetObjectiveCsvRows(ctx.Result)).TestResult);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RenderUsesGlobalCoordinatesAndReplacesItsOwnOverlays()
    {
        var parsed = RgbCrossResultParser.Parse(Measurement().ToString());
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            using var view = new ImageView();
            var context = new IProcessExecutionContext { ImageView = view, Result = new() { ViewResultJson = JsonConvert.SerializeObject(parsed) } };
            var baseline = view.ImageShow.Visuals.ToArray();
            var process = new RgbCrossProcess(); process.Render(context);
            Assert.Equal(baseline.Length + 63, view.ImageShow.Visuals.Count());
            Assert.Contains(view.ImageShow.Visuals.OfType<DVRectangle>(), r => r.Rect.X == 99 && r.Rect.Y == 85);
            process.Config.DrawRgbEdges = false; process.Render(context); Assert.Equal(baseline.Length + 9, view.ImageShow.Visuals.Count());
            process.Config.DrawPointLabels = false; process.Render(context); Assert.Equal(baseline, view.ImageShow.Visuals.ToArray());
        });
    }

    [Fact]
    public async Task ActualAlgorithmExportCanBeReadByProjectParser()
    {
        const int size = 400;
        byte[] pixels = new byte[size * size * 6];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) for (int channel = 0; channel < 3; channel++)
        {
            bool lit = false;
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
            {
                int dx = x - (100 + c * 100 + channel - 1), dy = y - (100 + r * 100);
                lit |= Math.Abs(dx) <= 1 && Math.Abs(dy) <= 16 || Math.Abs(dy) <= 1 && Math.Abs(dx) <= 16;
            }
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan((y * size + x) * 6 + channel * 2, 2), (ushort)(lit ? 50000 : 500));
        }
        using var image = new ColorVision.Algorithms.AlgorithmImageBuffer(size, size, size * 6, ColorVision.Algorithms.AlgorithmImageFormat.Bgr48, pixels);
        using var result = await ColorVision.ImageEditor.Algorithms.ImageAlgorithmPlatform.Runner.RunAsync(new ColorVision.Algorithms.AlgorithmRunRequest
        {
            Invocation = ColorVision.Algorithms.AlgorithmInvocation.Create(ColorVision.ImageEditor.Algorithms.DisplayMetrologyIds.RgbCrossRegistration, new ColorVision.ImageEditor.Algorithms.RgbCrossRegistrationParameters()),
            Inputs = [new() { Name = "source", Image = image, Ownership = ColorVision.Algorithms.AlgorithmInputOwnership.Borrowed, ColorSpace = "linear-device-values" }],
            RequiredCapabilities = ColorVision.Algorithms.AlgorithmHostCapabilities.Local | ColorVision.Algorithms.AlgorithmHostCapabilities.Headless
        });
        Assert.Equal(ColorVision.Algorithms.AlgorithmResultStatus.Succeeded, result.Status);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            ColorVision.ImageEditor.Algorithms.AlgorithmResultExporter.ExportJson(result, path);
            var parsed = RgbCrossResultParser.Parse(File.ReadAllText(path));
            Assert.Equal(9, parsed.Points.Count); Assert.All(parsed.Points, p => Assert.True(p.Valid));
            File.Delete(path);
            ColorVision.ImageEditor.Algorithms.RgbCrossMeasurementExporter.Export(result, path);
            parsed = RgbCrossResultParser.Parse(File.ReadAllText(path));
            Assert.Equal(size, parsed.ImageWidth); Assert.All(parsed.Points, p => Assert.Equal(2, p.MaximumEdgeSeparation));
            string? output = Environment.GetEnvironmentVariable("COLORVISION_RGB_CROSS_FIXTURE_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output); File.Copy(path, Path.Combine(output,"synthetic-csharp.json"),true); File.WriteAllBytes(Path.Combine(output,"synthetic.bgr"),pixels);
                foreach (string mode in new[] { "field-full", "field-roi" })
                {
                    string field = Path.Combine(output, mode, "measurement.json");
                    if (File.Exists(field)) { var actual = RgbCrossResultParser.Parse(File.ReadAllText(field)); Assert.Equal(7, actual.Points.Count(p => p.Valid)); Assert.Equal("INVALID", actual.Status); }
                }
            }

        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParserLivesWithExistingCrossAndConfigurationRoundTrips()
    {
        Assert.Equal(ProcessTypeCatalog.GetSubcategory(typeof(OpticCenterProcess)), ProcessTypeCatalog.GetSubcategory(typeof(RgbCrossProcess)));
        Assert.Equal(typeof(OpticCenterProcess).Namespace, typeof(RgbCrossProcess).Namespace);
        var p = new RgbCrossProcess(); p.Config.ResultJsonFile = @"C:\results\{BatchId}.json"; p.Config.RecipeConfig.EnableJudgment = true; p.Config.RecipeConfig.MaximumEdgeSeparationPixels = 2;
        var restored = new RgbCrossProcess(); restored.SetProcessConfig(JsonConvert.SerializeObject(p.Config));
        Assert.Equal(p.Config.ResultJsonFile, restored.Config.ResultJsonFile); Assert.Equal(2, restored.Config.RecipeConfig.MaximumEdgeSeparationPixels);
        Assert.EndsWith("123.json", RgbCrossProcess.ResolvePath(p.Config.ResultJsonFile, 123));
    }
    [Fact]
    public void BatchSelectorUsesFindCrossVersionTemplateAndExactDetail()
    {
        var old = new ColorVision.Engine.AlgResultMasterModel { Id = 1, BatchId = 5, ImgFileType = ColorVision.Engine.ViewResultAlgType.FindCross, version = "1.0" };
        var nine = new ColorVision.Engine.AlgResultMasterModel { Id = 2, BatchId = 5, TName = "Nine", ImgFileType = ColorVision.Engine.ViewResultAlgType.FindCross, version = "2.0", ResultCode = 0 };
        List<ColorVision.Engine.Templates.Jsons.DetailCommonModel> Details(int id) => [new() { PId = id, ResultJson = JsonConvert.SerializeObject(new ColorVision.Engine.Templates.Jsons.ResultFile { ResultFileName = @"C:\Results\nine.json" }) }];
        var input = RgbCrossBatchResults.Select([old,nine],5,"",Details); Assert.Equal(2,input.MasterId);
        Assert.Throws<InvalidDataException>(() => RgbCrossBatchResults.Select([nine],6,"",Details));
        Assert.Throws<InvalidDataException>(() => RgbCrossBatchResults.Select([nine],5,"other",Details));
        Assert.Throws<InvalidDataException>(() => RgbCrossBatchResults.Select([nine,nine],5,"",Details));
        nine.ResultCode=-1;Assert.Throws<InvalidDataException>(() => RgbCrossBatchResults.Select([nine],5,"",Details));
    }

    [Fact]
    public async Task BatchExecutionUsesPersistedJsonAndKeepsSourceMasterSnapshot()
    {
        string path = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        try
        {
            await File.WriteAllTextAsync(path,Measurement().ToString());
            var process = new RgbCrossProcess(new BatchResults(path));
            var ctx = new IProcessExecutionContext { Batch = new() { Id=5 }, Result = new() { Result=true }, ObjectiveTestResult=new() };
            Assert.True(await process.Execute(ctx));
            var snapshot = JsonConvert.DeserializeObject<RgbCrossViewResult>(ctx.Result.ViewResultJson)!;
            Assert.Equal(42,snapshot.SourceMasterId);Assert.Equal(path,snapshot.JsonFile);Assert.Equal("MEASURED",snapshot.Status);
            Assert.Equal(9,process.GetObjectiveCsvRows(ctx.Result).Count());
        }
        finally { File.Delete(path); }
    }
    private sealed class BatchResults(string path) : IRgbCrossBatchResults
    {
        public RgbCrossBatchInput Resolve(int batchId,string templateName) { Assert.Equal(5,batchId);return new(path,"",42); }
    }

    private static JObject CompactMeasurement()
    {
        var original = Measurement(); var points = new JArray();
        foreach (var item in original["points"]!)
        {
            var p = new JObject { ["id"] = points.Count + 1, ["separation"] = item["separation"]!["maximumEdgeSeparationPx"]!.DeepClone() };
            foreach (string channel in new[] { "R", "G", "B" })
            {
                var c = item["channels"]![channel]!; var arms = new JObject();
                foreach (var (name, key) in new[] { ("horizontal", "horizontalArm"), ("vertical", "verticalArm") })
                    arms[name] = new JArray(new[] { "x", "y", "width", "height" }.Select(k => c[key]![k]!.DeepClone()));
                p[channel] = arms;
            }
            points.Add(p);
        }
        return new JObject { ["width"] = 400, ["height"] = 400, ["points"] = points };
    }

    [Fact]
    public void IndependentCompactContractNeedsNoHostMetadata()
    {
        var parsed = RgbCrossResultParser.Parse(CompactMeasurement().ToString());
        Assert.Equal("MEASURED", parsed.Status); Assert.Equal(9, parsed.Points.Count);
        Assert.All(parsed.Points, p => { Assert.Equal(3, p.Channels.Count); Assert.Equal(1.5, p.MaximumEdgeSeparation); });
        var compact = CompactMeasurement(); compact["points"]![5]!["B"] = null; compact["points"]![5]!["separation"] = null;
        compact["points"]![5]!["reason"] = "B:ambiguous_arm_edges";
        parsed = RgbCrossResultParser.Parse(compact.ToString());
        Assert.Equal("INVALID", parsed.Status); Assert.Equal(2, parsed.Points[5].Channels.Count);
        string? sample = Environment.GetEnvironmentVariable("COLORVISION_RGB_CROSS_COMPACT_SAMPLE");
        if (!string.IsNullOrWhiteSpace(sample))
        {
            parsed = RgbCrossResultParser.Parse(File.ReadAllText(sample));
            Assert.Equal(7, parsed.Points.Count(p => p.Valid)); Assert.Equal("INVALID", parsed.Status);
        }
    }

    [Theory]
    [InlineData("id")]
    [InlineData("geometry")]
    [InlineData("separation")]
    [InlineData("channel")]
    [InlineData("missing")]
    public void CompactContractRejectsInconsistentMeasurements(string scenario)
    {
        var compact = CompactMeasurement(); var p = (JObject)compact["points"]![0]!;
        switch (scenario)
        {
            case "id": p["id"] = 2; break;
            case "geometry": p["R"]!["horizontal"]![0] = -1; break;
            case "separation": p["separation"] = 0; break;
            case "channel": p["B"] = null; break;
            case "missing": p.Remove("G"); break;
        }
        Assert.Throws<InvalidDataException>(() => RgbCrossResultParser.Parse(compact.ToString()));
    }

}
