using ColorVision.Engine.Templates.Jsons.DetectScreenDefects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process.ScreenDefects;
using System.IO;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class DetectScreenDefectsTests
    {
        private static readonly string[] ResultPropertyNames = { "AvgBrightness", "DefectCount", "GradeLevel", "TimeStamp", "Defects" };
        private static readonly string[] DefectPropertyNames = { "Id", "Type", "X", "Y", "Width", "Height", "Area", "Contrast", "MeanValue", "LocalMean" };

        [Fact]
        public void ParserReadsDirectAlgorithmJson()
        {
            const string json = """
                {
                  "AvgBrightness": 125.5,
                  "DefectCount": 1,
                  "GradeLevel": "OK",
                  "Defects": [
                    { "type": "point", "x": 10, "y": 20, "width": 3, "height": 4, "area": 12 }
                  ]
                }
                """;

            DetectScreenDefectsResult? result = ScreenDefectsResultParser.Parse(json, out string? resultFileName);

            Assert.NotNull(result);
            Assert.Null(resultFileName);
            Assert.Equal(125.5, result.AvgBrightness);
            Assert.Single(result.Defects);
            Assert.Equal("point", result.Defects[0].Type);
        }

        [Fact]
        public void ParserReadsAlgorithmJsonFromResultFileWrapper()
        {
            string filePath = Path.Combine(Path.GetTempPath(), $"screen-defects-{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(filePath, """
                    {
                      "DefectCount": 1,
                      "Defects": [
                        { "type": "line", "x": 1, "y": 2, "width": 30, "height": 2, "area": 60 }
                      ]
                    }
                    """);
                string wrapper = JsonConvert.SerializeObject(new { ResultFileName = filePath });

                DetectScreenDefectsResult? result = ScreenDefectsResultParser.Parse(wrapper, out string? resultFileName);

                Assert.NotNull(result);
                Assert.Equal(filePath, resultFileName);
                Assert.Equal("line", Assert.Single(result.Defects).Type);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [Fact]
        public void CreateCleanResultKeepsOnlyCustomerFacingValues()
        {
            var source = new DetectScreenDefectsResult
            {
                AvgBrightness = 125.5,
                DefectCount = 1,
                GradeLevel = "OK",
                TimeStamp = "2026-07-17 10:00:00",
                Defects = new List<DetectScreenDefectItem>
                {
                    new()
                    {
                        Type = "point",
                        X = 10,
                        Y = 20,
                        Width = 3,
                        Height = 4,
                        Area = 12,
                        Contrast = 0.25,
                        MeanValue = 80,
                        LocalMean = 100
                    }
                }
            };

            ScreenDefectsData result = DetectScreenDefectsProcess.CreateCleanResult(source);
            JObject json = JObject.Parse(JsonConvert.SerializeObject(result));

            Assert.Equal(ResultPropertyNames, json.Properties().Select(property => property.Name));
            Assert.Equal(DefectPropertyNames, ((JObject)json["Defects"]![0]!).Properties().Select(property => property.Name));
            Assert.Equal(1, result.Defects[0].Id);
            Assert.DoesNotContain("ResultFileName", json.ToString());
            Assert.DoesNotContain("DetailCommonModel", json.ToString());
            Assert.DoesNotContain("LowLimit", json.ToString());
            Assert.DoesNotContain("UpLimit", json.ToString());
        }

        [Fact]
        public void ObjectiveTestResultStoresNamedScreenDefectsAsOneCleanValue()
        {
            var objectiveResult = new ObjectiveTestResult();
            objectiveResult.DynamicScreenDefectResults["WhiteScreen"] = new ScreenDefectsData
            {
                DefectCount = 1,
                Defects = new List<ScreenDefectData>
                {
                    new() { Id = 1, Type = "line", X = 1, Y = 2, Width = 30, Height = 2, Area = 60 }
                }
            };

            JObject json = JObject.Parse(JsonConvert.SerializeObject(objectiveResult));
            JToken? screenResult = json["DynamicScreenDefectResults"]?["WhiteScreen"];

            Assert.NotNull(screenResult);
            Assert.Equal(1, screenResult!["DefectCount"]!.Value<int>());
            Assert.Single(screenResult["Defects"]!);
            Assert.Null(screenResult["AvgBrightness"]);
            Assert.DoesNotContain("TestResult", screenResult.ToString());
        }

        [Fact]
        public void BuildTextShowsSummaryAndDefectParametersWithoutLimitColumns()
        {
            var result = new ScreenDefectsData
            {
                AvgBrightness = 100,
                DefectCount = 1,
                GradeLevel = "OK",
                Defects = new List<ScreenDefectData>
                {
                    new() { Id = 1, Type = "point", X = 10, Y = 20, Width = 3, Height = 4, Area = 12 }
                }
            };

            string text = DetectScreenDefectsProcess.BuildText("WhiteScreen", result);

            Assert.Contains("WhiteScreen 屏幕缺陷检测结果", text);
            Assert.Contains("DefectCount:1", text);
            Assert.Contains("1,point,10.0000,20.0000,3.0000,4.0000,12.0000", text);
            Assert.DoesNotContain("LowLimit", text);
            Assert.DoesNotContain("UpLimit", text);
            Assert.DoesNotContain("PASS", text);
        }
    }
}
