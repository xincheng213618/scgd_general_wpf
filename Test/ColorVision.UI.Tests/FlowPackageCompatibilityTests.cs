using ColorVision.Engine;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

public class FlowPackageCompatibilityTests
{
    [Fact]
    public void TemplateExtractionAndReplacementAcceptVersionOne()
    {
        StaTest.Run(() =>
        {
            byte[] flowData = CreateTemplateReferenceFlow("Camera.Template.A");

            Assert.Contains("Camera.Template.A", FlowPackageHelper.ExtractTemplateNames(flowData));

            byte[] replaced = FlowPackageHelper.ReplaceTemplateNames(
                flowData,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Camera.Template.A"] = "Camera.Template.B",
                });

            Assert.DoesNotContain("Camera.Template.A", FlowPackageHelper.ExtractTemplateNames(replaced));
            Assert.Contains("Camera.Template.B", FlowPackageHelper.ExtractTemplateNames(replaced));
            Assert.Equal(1, replaced[4]);
        });
    }

    [Fact]
    public void UnknownStnVersionIsRejectedWithoutRewritingInput()
    {
        StaTest.Run(() =>
        {
            byte[] flowData = CreateTemplateReferenceFlow("Camera.Template.A");
            flowData[4] = 2;
            byte[] original = flowData.ToArray();

            Assert.Empty(FlowPackageHelper.ExtractTemplateNames(flowData));

            byte[] replacementResult = FlowPackageHelper.ReplaceTemplateNames(
                flowData,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Camera.Template.A"] = "Camera.Template.B",
                });

            Assert.Same(flowData, replacementResult);
            Assert.Equal(original, replacementResult);
        });
    }

    [Fact]
    public void TemplateExtractionCoversCurrentReferencePropertyNames()
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor();
            var node = new AlternateTemplateReferenceNode
            {
                AutoExpTempName = "Exposure-A",
                FocusTempName = "Focus-A",
                OutputTempName = "Output-A",
                LayoutROITemplateName = "Layout-A",
                ParameterTemplateName = "Parameter-A",
                SubPixelTemplateName = "SubPixel-A",
            };
            node.Create();
            editor.Nodes.Add(node);

            byte[] flowData = editor.GetCanvasData();
            HashSet<string> names =
                FlowPackageHelper.ExtractTemplateNames(flowData);
            Assert.Equal(
                6,
                names.Intersect(
                    [
                        "Exposure-A",
                        "Focus-A",
                        "Output-A",
                        "Layout-A",
                        "Parameter-A",
                        "SubPixel-A",
                    ],
                    StringComparer.OrdinalIgnoreCase)
                    .Count());
        });
    }

    [Fact]
    public void VersionThreePackageStoresAndValidatesTemplatePayloads()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".cvflow");
        byte[] stnData = CreateValidFlowData();
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
            Templates =
            {
                new FlowPackageTemplate
                {
                    TemplateName = "Camera-A",
                    TemplateCode = "Camera",
                    TemplateDicId = 42,
                    SerializedContent =
                        """{"Id":7,"Name":"Camera-A","Exposure":80}""",
                    Details =
                    {
                        new FlowPackageDetailItem
                        {
                            Symbol = "Exposure",
                            AddressCode = 11,
                            ValueA = "80",
                        },
                    },
                },
            },
        };

        try
        {
            FlowPackageHelper.ExportFlowPackage(
                filePath,
                manifest.FlowName,
                stnData,
                manifest);

            using (var archive = ZipFile.OpenRead(filePath))
            {
                FlowPackageManifest stored =
                    ReadManifest(archive);
                Assert.Equal(
                    "colorvision.cvflow",
                    stored.Schema);
                Assert.Equal("3.0", stored.Version);
                Assert.NotEmpty(stored.FlowContentHash);
                FlowPackageTemplate metadata =
                    Assert.Single(stored.Templates);
                Assert.Null(metadata.SerializedContent);
                Assert.Empty(metadata.Details);
                Assert.NotEmpty(metadata.ContentHash);
                Assert.NotEmpty(metadata.PayloadHash);
                Assert.NotNull(
                    archive.GetEntry(metadata.ContentEntry!));
            }

            var imported =
                FlowPackageHelper.ImportFlowPackage(filePath);
            Assert.Equal(stnData, imported.StnData);
            Assert.NotNull(imported.Manifest);
            FlowPackageTemplate hydrated =
                Assert.Single(imported.Manifest!.Templates);
            Assert.Contains(
                "\"Exposure\":80",
                hydrated.SerializedContent);
            Assert.Single(hydrated.Details);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void VersionThreePackageRejectsTamperedTemplatePayload()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".cvflow");
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
            Templates =
            {
                PackageTemplate(
                    "Camera-A",
                    exposure: 80),
            },
        };

        try
        {
            FlowPackageHelper.ExportFlowPackage(
                filePath,
                manifest.FlowName,
                CreateValidFlowData(),
                manifest);
            using (ZipArchive archive = ZipFile.Open(
                filePath,
                ZipArchiveMode.Update))
            {
                FlowPackageManifest stored =
                    ReadManifest(archive);
                string entryName =
                    Assert.Single(stored.Templates).ContentEntry!;
                archive.GetEntry(entryName)!.Delete();
                ZipArchiveEntry replacement =
                    archive.CreateEntry(entryName);
                using var writer = new StreamWriter(
                    replacement.Open(),
                    new UTF8Encoding(false));
                writer.Write("{}");
            }

            Assert.Throws<InvalidDataException>(() =>
                FlowPackageHelper.ImportFlowPackage(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void VersionThreePackageRejectsTamperedTemplateIdentity()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".cvflow");
        try
        {
            FlowPackageHelper.ExportFlowPackage(
                filePath,
                "Flow-A",
                CreateValidFlowData(),
                new FlowPackageManifest
                {
                    FlowName = "Flow-A",
                    Templates =
                    {
                        PackageTemplate(
                            "Camera-A",
                            exposure: 80),
                    },
                });
            using (ZipArchive archive = ZipFile.Open(
                filePath,
                ZipArchiveMode.Update))
            {
                FlowPackageManifest stored =
                    ReadManifest(archive);
                stored.Templates[0].PackageTemplateId =
                    new string('a', 64);
                archive.GetEntry("manifest.json")!.Delete();
                WriteEntry(
                    archive,
                    "manifest.json",
                    Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(stored)));
            }

            Assert.Throws<InvalidDataException>(() =>
                FlowPackageHelper.ImportFlowPackage(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void VersionThreePackageRejectsTamperedFlowPayload()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".cvflow");
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
        };

        try
        {
            FlowPackageHelper.ExportFlowPackage(
                filePath,
                manifest.FlowName,
                CreateValidFlowData(),
                manifest);
            using (ZipArchive archive = ZipFile.Open(
                filePath,
                ZipArchiveMode.Update))
            {
                archive.GetEntry("flow.stn")!.Delete();
                WriteEntry(
                    archive,
                    "flow.stn",
                    [3, 2, 1]);
            }

            Assert.Throws<InvalidDataException>(() =>
                FlowPackageHelper.ImportFlowPackage(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void VersionThreeRejectsHashConsistentInvalidStn()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".cvflow");
        byte[] invalidStn = [1, 2, 3];
        var manifest = new FlowPackageManifest
        {
            Schema = "colorvision.cvflow",
            Version = "3.0",
            FlowName = "Invalid",
            PackageId = Guid.NewGuid().ToString("N"),
            CreatedUtc = DateTime.UtcNow,
            FlowContentHash = Convert.ToHexString(
                    SHA256.HashData(invalidStn))
                .ToLowerInvariant(),
        };

        try
        {
            using (ZipArchive archive = ZipFile.Open(
                filePath,
                ZipArchiveMode.Create))
            {
                WriteEntry(
                    archive,
                    "flow.stn",
                    invalidStn);
                WriteEntry(
                    archive,
                    "manifest.json",
                    Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(manifest)));
            }

            Assert.Throws<InvalidDataException>(() =>
                FlowPackageHelper.ImportFlowPackage(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void FuturePackageMajorVersionIsRejected()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".cvflow");
        var manifest = new FlowPackageManifest
        {
            Version = "4.0",
            FlowName = "Future",
        };

        try
        {
            using (ZipArchive archive = ZipFile.Open(
                filePath,
                ZipArchiveMode.Create))
            {
                WriteEntry(
                    archive,
                    "flow.stn",
                    CreateValidFlowData());
                WriteEntry(
                    archive,
                    "manifest.json",
                    Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(manifest)));
            }

            Assert.Throws<NotSupportedException>(() =>
                FlowPackageHelper.ImportFlowPackage(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void LegacyInlinePackageRemainsImportable()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N") + ".cvflow");
        var manifest = new FlowPackageManifest
        {
            Version = "2.0",
            FlowName = "Legacy",
            PackageId = string.Empty,
            CreatedUtc = default,
            Templates =
            {
                PackageTemplate(
                    "Camera-A",
                    exposure: 80),
            },
        };
        manifest.FlowContentHash = string.Empty;

        try
        {
            using (ZipArchive archive = ZipFile.Open(
                filePath,
                ZipArchiveMode.Create))
            {
                WriteEntry(
                    archive,
                    "flow.stn",
                    CreateValidFlowData());
                WriteEntry(
                    archive,
                    "manifest.json",
                    Encoding.UTF8.GetBytes(
                        JsonConvert.SerializeObject(manifest)));
            }

            var imported =
                FlowPackageHelper.ImportFlowPackage(filePath);
            FlowPackageTemplate template =
                Assert.Single(imported.Manifest!.Templates);
            Assert.Contains(
                "\"Exposure\":80",
                template.SerializedContent);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void SameNameWithEquivalentContentUsesExistingTemplate()
    {
        var template = new FakeAssociatedTemplate(
            ("Camera-A", 80));
        var catalog = new Dictionary<string, ITemplate>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Camera"] = template,
        };
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
            Templates =
            {
                PackageTemplate(
                    "Camera-A",
                    exposure: 80),
            },
        };

        try
        {
            Dictionary<string, string> result =
                FlowPackageHelper.ImportTemplates(
                    manifest,
                    manifest.FlowName,
                    catalog);

            Assert.Empty(result);
            Assert.Equal(0, template.CreateCount);
            Assert.Equal(1, template.Count);
        }
        finally
        {
            RemoveTemplateRegistration(template);
        }
    }

    [Fact]
    public void DetailAuditFieldsDoNotChangeSemanticIdentity()
    {
        var first = new[]
        {
            new FlowPackageDetailItem
            {
                SysPid = 11,
                ValueA = "80",
                ValueB = "40",
                IsEnable = true,
                IsDelete = false,
            },
        };
        var second = new[]
        {
            new FlowPackageDetailItem
            {
                SysPid = 11,
                ValueA = "80",
                ValueB = "70",
                IsEnable = false,
                IsDelete = true,
            },
        };

        Assert.Equal(
            FlowPackageContentIdentity.ComputeContentHash(
                "Camera",
                null,
                first),
            FlowPackageContentIdentity.ComputeContentHash(
                "Camera",
                null,
                second));
    }

    [Fact]
    public void StableDetailIdentityIgnoresDatabaseIdsAndMapsLocally()
    {
        var packaged = new FlowPackageDetailItem
        {
            SysPid = 11,
            Symbol = "Exposure",
            AddressCode = 101,
            ValueA = "80",
        };
        var sameValueFromAnotherDatabase =
            new FlowPackageDetailItem
            {
                SysPid = 9001,
                Symbol = "Exposure",
                AddressCode = 101,
                ValueA = "80",
            };
        var localDefinition =
            new SysDictionaryModDetaiModel
            {
                Id = 42,
                PId = 7,
                Symbol = "Exposure",
                AddressCode = 101,
            };

        Assert.Equal(
            FlowPackageContentIdentity.ComputeContentHash(
                "Camera",
                null,
                [packaged]),
            FlowPackageContentIdentity.ComputeContentHash(
                "Camera",
                null,
                [sameValueFromAnotherDatabase]));
        Assert.Equal(
            42,
            FlowPackageHelper.ResolveLocalDetailSystemId(
                packaged,
                [localDefinition]));
    }

    [Fact]
    public void CaseOnlyEquivalentNameReturnsExactReferenceMapping()
    {
        var template = new FakeAssociatedTemplate(
            ("Camera-A", 80));
        var catalog = new Dictionary<string, ITemplate>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Camera"] = template,
        };
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
            Templates =
            {
                PackageTemplate(
                    "camera-a",
                    exposure: 80),
            },
        };

        try
        {
            Dictionary<string, string> result =
                FlowPackageHelper.ImportTemplates(
                    manifest,
                    manifest.FlowName,
                    catalog);

            Assert.Equal(
                "Camera-A",
                result["camera-a"]);
            byte[] rewritten = FlowPackageHelper
                .ReplaceTemplateNames(
                    CreateValidFlowData("camera-a"),
                    result);
            Assert.Equal(
                "Camera-A",
                Assert.Single(
                    FlowPackageHelper.ExtractTemplateNames(
                        rewritten)));
            Assert.Equal(0, template.CreateCount);
        }
        finally
        {
            RemoveTemplateRegistration(template);
        }
    }

    [Fact]
    public void FailedTemplateCreationAbortsPackageImport()
    {
        var template = new FailingAssociatedTemplate();
        var catalog = new Dictionary<string, ITemplate>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Camera"] = template,
        };
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
            Templates =
            {
                PackageTemplate(
                    "Camera-A",
                    exposure: 80),
            },
        };

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                FlowPackageHelper.ImportTemplates(
                    manifest,
                    manifest.FlowName,
                    catalog));
            Assert.Empty(template.GetTemplateNames());
        }
        finally
        {
            RemoveTemplateRegistration(template);
        }
    }

    [Fact]
    public void RepeatedImportReusesEquivalentConflictCopy()
    {
        var template = new FakeAssociatedTemplate(
            ("Camera-A", 40));
        var catalog = new Dictionary<string, ITemplate>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Camera"] = template,
        };
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
            Templates =
            {
                PackageTemplate(
                    "Camera-A",
                    exposure: 80),
            },
        };

        try
        {
            Dictionary<string, string> first =
                FlowPackageHelper.ImportTemplates(
                    manifest,
                    manifest.FlowName,
                    catalog);
            Assert.Equal(
                "Camera-A_Flow-A",
                first["Camera-A"]);
            Assert.Equal(1, template.CreateCount);

            Dictionary<string, string> second =
                FlowPackageHelper.ImportTemplates(
                    manifest,
                    manifest.FlowName,
                    catalog);
            Assert.Equal(
                "Camera-A_Flow-A",
                second["Camera-A"]);
            Assert.Equal(1, template.CreateCount);
            Assert.Equal(2, template.Count);
        }
        finally
        {
            RemoveTemplateRegistration(template);
        }
    }

    [Fact]
    public void RepeatedImportReusesSelfReferencingConflictCopy()
    {
        var template = new FakeAssociatedTemplate(
            ("Camera-A", 40));
        var catalog = new Dictionary<string, ITemplate>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Camera"] = template,
        };
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
            Templates =
            {
                PackageTemplate(
                    "Camera-A",
                    exposure: 80,
                    referenceName: "Camera-A"),
            },
        };

        try
        {
            Dictionary<string, string> first =
                FlowPackageHelper.ImportTemplates(
                    manifest,
                    manifest.FlowName,
                    catalog);
            Dictionary<string, string> second =
                FlowPackageHelper.ImportTemplates(
                    manifest,
                    manifest.FlowName,
                    catalog);

            Assert.Equal(
                "Camera-A_Flow-A",
                first["Camera-A"]);
            Assert.Equal(
                "Camera-A_Flow-A",
                second["Camera-A"]);
            Assert.Equal(1, template.CreateCount);
            Assert.Equal(2, template.Count);
        }
        finally
        {
            RemoveTemplateRegistration(template);
        }
    }

    [Fact]
    public void ReferenceRewriteDoesNotChangeOrdinaryStrings()
    {
        const string json =
            """
            {
              "SaveName": "Camera-A",
              "TemplateName": "Camera-A",
              "Nested": "{\"AutoExpTempName\":\"Camera-A\",\"Note\":\"Camera-A\"}"
            }
            """;
        string rewritten =
            FlowPackageHelper.ReplaceTemplateReferencesInJsonContent(
                json,
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["Camera-A"] = "Camera-B",
                })!;
        JObject root = JObject.Parse(rewritten);

        Assert.Equal("Camera-A", root["SaveName"]!.Value<string>());
        Assert.Equal("Camera-B", root["TemplateName"]!.Value<string>());
        JObject nested = JObject.Parse(
            root["Nested"]!.Value<string>()!);
        Assert.Equal(
            "Camera-B",
            nested["AutoExpTempName"]!.Value<string>());
        Assert.Equal(
            "Camera-A",
            nested["Note"]!.Value<string>());
    }

    [Fact]
    public void DifferentNameWithEquivalentContentUsesExistingTemplate()
    {
        var template = new FakeAssociatedTemplate(
            ("Local-Camera", 80));
        var catalog = new Dictionary<string, ITemplate>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["Camera"] = template,
        };
        var manifest = new FlowPackageManifest
        {
            FlowName = "Flow-A",
            Templates =
            {
                PackageTemplate(
                    "Packaged-Camera",
                    exposure: 80),
            },
        };

        try
        {
            Dictionary<string, string> result =
                FlowPackageHelper.ImportTemplates(
                    manifest,
                    manifest.FlowName,
                    catalog);

            Assert.Equal(
                "Local-Camera",
                result["Packaged-Camera"]);
            Assert.Equal(0, template.CreateCount);
        }
        finally
        {
            RemoveTemplateRegistration(template);
        }
    }

    private static FlowPackageTemplate PackageTemplate(
        string name,
        int exposure,
        string? referenceName = null)
    {
        return new FlowPackageTemplate
        {
            TemplateName = name,
            TemplateCode = "Camera",
            SerializedContent = JsonConvert.SerializeObject(
                new FakeTemplateValue
                {
                    Id = 100,
                    Name = name,
                    Exposure = exposure,
                    CamTempName = referenceName
                        ?? string.Empty,
                }),
        };
    }

    private static FlowPackageManifest ReadManifest(
        ZipArchive archive)
    {
        using Stream stream =
            archive.GetEntry("manifest.json")!.Open();
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8);
        return JsonConvert.DeserializeObject<
            FlowPackageManifest>(
                reader.ReadToEnd())!;
    }

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        byte[] content)
    {
        using Stream stream =
            archive.CreateEntry(name).Open();
        stream.Write(content);
    }

    private static void RemoveTemplateRegistration(
        ITemplate template)
    {
        if (TemplateControl.ITemplateNames.TryGetValue(
                template.Name,
                out ITemplate? registered)
            && ReferenceEquals(registered, template))
        {
            TemplateControl.ITemplateNames.Remove(
                template.Name);
        }
    }

    private static byte[] CreateTemplateReferenceFlow(string templateName)
    {
        using var editor = new STNodeEditor();
        var node = new TemplateReferenceNode
        {
            TempName = templateName,
        };
        node.Create();
        editor.Nodes.Add(node);
        return editor.GetCanvasData();
    }

    private static byte[] CreateValidFlowData(
        string templateName = "Camera.Template.A")
    {
        byte[]? flowData = null;
        StaTest.Run(() =>
        {
            flowData = CreateTemplateReferenceFlow(
                templateName);
        });
        return flowData!;
    }

    private sealed class TemplateReferenceNode : STNode
    {
        [STNodeProperty("Template", "Template reference")]
        public string TempName { get; set; } = string.Empty;
    }

    private sealed class AlternateTemplateReferenceNode : STNode
    {
        [STNodeProperty("Auto exposure", "Template reference")]
        public string AutoExpTempName { get; set; } = string.Empty;

        [STNodeProperty("Focus", "Template reference")]
        public string FocusTempName { get; set; } = string.Empty;

        [STNodeProperty("Output", "Template reference")]
        public string OutputTempName { get; set; } = string.Empty;

        [STNodeProperty("Layout", "Template reference")]
        public string LayoutROITemplateName { get; set; } =
            string.Empty;

        [STNodeProperty("Parameter", "Template reference")]
        public string ParameterTemplateName { get; set; } =
            string.Empty;

        [STNodeProperty("Sub-pixel", "Template reference")]
        public string SubPixelTemplateName { get; set; } =
            string.Empty;
    }

    private sealed class FakeAssociatedTemplate : ITemplate
    {
        private readonly List<FakeTemplateValue> values = new();
        private FakeTemplateValue? pending;

        public FakeAssociatedTemplate(
            params (string Name, int Exposure)[] templates)
        {
            foreach ((string name, int exposure) in templates)
            {
                values.Add(
                    new FakeTemplateValue
                    {
                        Id = values.Count + 1,
                        Name = name,
                        Exposure = exposure,
                    });
            }
        }

        public int CreateCount { get; private set; }

        public override int Count => values.Count;

        public override List<string> GetTemplateNames()
        {
            return values.Select(value => value.Name).ToList();
        }

        public override int GetTemplateIndex(
            string templateName)
        {
            return values.FindIndex(value =>
                value.Name.Equals(
                    templateName,
                    StringComparison.Ordinal));
        }

        public override object GetParamValue(int index)
        {
            return values[index];
        }

        public override bool ImportJsonContent(
            string templateName,
            string jsonContent)
        {
            pending =
                JsonConvert.DeserializeObject<
                    FakeTemplateValue>(jsonContent);
            return pending != null;
        }

        public override void Create(string templateName)
        {
            Assert.NotNull(pending);
            pending!.Id = values.Count + 1;
            pending.Name = templateName;
            values.Add(pending);
            pending = null;
            CreateCount++;
        }

        public override void ClearCreateTemplateSource()
        {
            pending = null;
        }
    }

    private sealed class FailingAssociatedTemplate : ITemplate
    {
        public override int Count => 0;

        public override List<string> GetTemplateNames()
        {
            return new List<string>();
        }

        public override bool ImportJsonContent(
            string templateName,
            string jsonContent)
        {
            return false;
        }

        public override void Create(string templateName)
        {
        }

        public override bool TryCreateTemplate(
            string templateName,
            out string message)
        {
            message = "simulated creation failure";
            return false;
        }
    }

    private sealed class FakeTemplateValue
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Exposure { get; set; }
        public string CamTempName { get; set; } = string.Empty;
    }
}
