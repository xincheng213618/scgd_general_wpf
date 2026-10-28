using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Recipe;
using System.IO;

namespace ProjectARVRPro.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessManagerPersistenceTestGroup
{
    public const string Name = "ProjectARVRPro process persistence";
}

[Collection(ProcessManagerPersistenceTestGroup.Name)]
public sealed class ProcessManagerPersistenceTests
{
    [Fact]
    public void CameraOverrideMappingRequiresOneProcessForTheFlowTemplate()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            var manager = new ProcessManager();
            ProcessGroup group = Assert.Single(manager.ProcessGroups);
            var first = new ProcessMeta
            {
                Name = "First",
                FlowTemplate = "SharedFlow",
                Process = new BlackProcess()
            };
            group.ProcessMetas.Add(first);

            Assert.Same(first, manager.FindUniqueProcessMetaForTemplate("sharedflow"));

            group.ProcessMetas.Add(new ProcessMeta
            {
                Name = "Second",
                FlowTemplate = "SharedFlow",
                Process = new BlackProcess()
            });

            Assert.Null(manager.FindUniqueProcessMetaForTemplate("SharedFlow"));
        });
    }

    [Fact]
    public void DuplicateMetaCreatesIndependentConfiguredCopyAfterSource()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            var manager = new ProcessManager();
            ProcessGroup group = Assert.Single(manager.ProcessGroups);
            var sourceProcess = new BlackProcess();
            sourceProcess.Config.RecipeConfig.FOFOContrast.Min = 123;
            var source = new ProcessMeta
            {
                Name = "Source",
                FlowTemplate = "SourceTemplate",
                SocketCode = "21",
                Process = sourceProcess,
                IsEnabled = false,
                ConfigJson = JsonConvert.SerializeObject(sourceProcess.Config),
                PictureSwitchConfig = new PictureSwitchConfig
                {
                    IsEnabled = true,
                    SendCommand = "PIC9",
                    SuccessDelayMs = 900
                },
                FlowCameraParameterOverrideConfig = new FlowCameraParameterOverrideConfig
                {
                    IsEnabled = true,
                    ExposureTimeMs = 15.5f,
                    CalibrationTemplateName = "CalibrationA"
                }
            };
            group.ProcessMetas.Add(source);
            group.ProcessMetas.Add(new ProcessMeta { Name = "Source_Copy" });
            manager.SelectedProcessMeta = source;

            manager.DuplicateMetaCommand.Execute(null);

            Assert.Equal(3, group.ProcessMetas.Count);
            ProcessMeta copy = group.ProcessMetas[1];
            Assert.Same(copy, manager.SelectedProcessMeta);
            Assert.Equal("Source_Copy_1", copy.Name);
            Assert.Equal(source.FlowTemplate, copy.FlowTemplate);
            Assert.Equal("21", copy.SocketCode);
            Assert.Equal(source.IsEnabled, copy.IsEnabled);
            Assert.NotSame(source.Process, copy.Process);
            BlackProcess copiedProcess = Assert.IsType<BlackProcess>(copy.Process);
            Assert.Equal(123, copiedProcess.Config.RecipeConfig.FOFOContrast.Min);
            Assert.NotSame(sourceProcess.Config.RecipeConfig, copiedProcess.Config.RecipeConfig);
            Assert.NotSame(source.PictureSwitchConfig, copy.PictureSwitchConfig);
            Assert.True(copy.PictureSwitchConfig.IsEnabled);
            Assert.Equal("PIC9", copy.PictureSwitchConfig.SendCommand);
            Assert.Equal(900, copy.PictureSwitchConfig.SuccessDelayMs);
            Assert.NotSame(source.FlowCameraParameterOverrideConfig, copy.FlowCameraParameterOverrideConfig);
            Assert.True(copy.FlowCameraParameterOverrideConfig.IsEnabled);
            Assert.Equal(15.5f, copy.FlowCameraParameterOverrideConfig.ExposureTimeMs);
            Assert.Equal("CalibrationA", copy.FlowCameraParameterOverrideConfig.CalibrationTemplateName);

            copiedProcess.Config.RecipeConfig.FOFOContrast.Min = 456;
            copy.PictureSwitchConfig.SendCommand = "PICA";
            copy.FlowCameraParameterOverrideConfig.ExposureTimeMs = 88f;
            copy.FlowCameraParameterOverrideConfig.CalibrationTemplateName = "CalibrationB";
            Assert.Equal(123, sourceProcess.Config.RecipeConfig.FOFOContrast.Min);
            Assert.Equal("PIC9", source.PictureSwitchConfig.SendCommand);
            Assert.Equal(15.5f, source.FlowCameraParameterOverrideConfig.ExposureTimeMs);
            Assert.Equal("CalibrationA", source.FlowCameraParameterOverrideConfig.CalibrationTemplateName);
        });
    }

    [Fact]
    public void MoveMetaToIndexReordersActiveGroupAndKeepsSelection()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            var manager = new ProcessManager();
            ProcessGroup group = Assert.Single(manager.ProcessGroups);
            var first = new ProcessMeta { Name = "First" };
            var second = new ProcessMeta { Name = "Second" };
            var third = new ProcessMeta { Name = "Third" };
            group.ProcessMetas.Add(first);
            group.ProcessMetas.Add(second);
            group.ProcessMetas.Add(third);

            Assert.True(manager.MoveMetaToIndex(first, 2));

            Assert.Equal("Second,Third,First", string.Join(',', group.ProcessMetas.Select(meta => meta.Name)));
            Assert.Same(first, manager.SelectedProcessMeta);
            Assert.False(manager.MoveMetaToIndex(first, 2));
        });
    }

    [Fact]
    public void SaveReloadDuplicateAndResultParserKeepIndependentRecipeValues()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            var manager = new ProcessManager();
            ProcessGroup originalGroup = Assert.Single(manager.ProcessGroups);
            var source = new BlackProcess();
            source.Config.RecipeConfig.FOFOContrast.Min = 111;
            originalGroup.ProcessMetas.Add(new ProcessMeta
            {
                Name = "Source",
                FlowTemplate = "SourceTemplate",
                SocketCode = "22",
                Process = source,
                ConfigJson = JsonConvert.SerializeObject(new BlackProcessConfig()),
                FlowCameraParameterOverrideConfig = new FlowCameraParameterOverrideConfig
                {
                    IsEnabled = true,
                    ExposureTimeMs = 25f,
                    CalibrationTemplateName = "CalibrationA"
                }
            });

            manager.DuplicateGroupCommand.Execute(null);

            var parser = new BlackProcess();
            parser.Config.RecipeConfig.FOFOContrast.Min = 222;
            manager.ResultParserMetas.Add(new ProcessMeta
            {
                Name = "Parser",
                FlowTemplate = "ParserTemplate",
                Process = parser,
                ConfigJson = JsonConvert.SerializeObject(parser.Config)
            });
            Assert.True(manager.TrySaveProcessGroups());

            var reloaded = new ProcessManager();
            Assert.Equal(2, reloaded.ProcessGroups.Count);
            ProcessMeta restoredSourceMeta = Assert.Single(reloaded.ProcessGroups[0].ProcessMetas);
            ProcessMeta restoredCopyMeta = Assert.Single(reloaded.ProcessGroups[1].ProcessMetas);
            Assert.Equal("22", restoredSourceMeta.SocketCode);
            Assert.Equal("22", restoredCopyMeta.SocketCode);
            Assert.Same(restoredCopyMeta, reloaded.FindProcessMetaBySocketCode("22"));
            restoredCopyMeta.IsEnabled = false; // Legacy Socket dispatch is independent of RunAll's enabled list.
            Assert.Same(restoredCopyMeta, reloaded.FindProcessMetaBySocketCode("22"));
            Assert.Null(reloaded.FindProcessMetaBySocketCode("99"));
            BlackProcess restoredSource = Assert.IsType<BlackProcess>(restoredSourceMeta.Process);
            BlackProcess restoredCopy = Assert.IsType<BlackProcess>(restoredCopyMeta.Process);
            BlackProcess restoredParser = Assert.IsType<BlackProcess>(Assert.Single(reloaded.ResultParserMetas).Process);

            Assert.Equal(111, restoredSource.Config.RecipeConfig.FOFOContrast.Min);
            Assert.Equal(111, restoredCopy.Config.RecipeConfig.FOFOContrast.Min);
            Assert.Equal(222, restoredParser.Config.RecipeConfig.FOFOContrast.Min);
            Assert.NotSame(restoredSource.Config.RecipeConfig, restoredCopy.Config.RecipeConfig);
            Assert.NotSame(restoredSource.Config.RecipeConfig.FOFOContrast, restoredCopy.Config.RecipeConfig.FOFOContrast);
            Assert.NotSame(restoredSource.Config.RecipeConfig, restoredParser.Config.RecipeConfig);
            Assert.NotSame(restoredSourceMeta.FlowCameraParameterOverrideConfig, restoredCopyMeta.FlowCameraParameterOverrideConfig);
            Assert.True(restoredSourceMeta.FlowCameraParameterOverrideConfig.IsEnabled);
            Assert.True(restoredCopyMeta.FlowCameraParameterOverrideConfig.IsEnabled);
            Assert.Equal(25f, restoredSourceMeta.FlowCameraParameterOverrideConfig.ExposureTimeMs);
            Assert.Equal(25f, restoredCopyMeta.FlowCameraParameterOverrideConfig.ExposureTimeMs);
            Assert.Equal("CalibrationA", restoredSourceMeta.FlowCameraParameterOverrideConfig.CalibrationTemplateName);
            Assert.Equal("CalibrationA", restoredCopyMeta.FlowCameraParameterOverrideConfig.CalibrationTemplateName);
        });
    }

    [Fact]
    public void ReloadingConfigurationWithoutCameraOverrideCreatesDisabledDefaults()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            Directory.CreateDirectory(ViewResultManager.DirectoryPath);
            string filePath = Path.Combine(ViewResultManager.DirectoryPath, ProcessManager.GroupPersistFileName);
            var process = new BlackProcess();
            var legacyRoot = new ProcessGroupsRoot
            {
                Version = 3,
                Groups = new List<ProcessGroupPersist>
                {
                    new()
                    {
                        Name = "Legacy",
                        Metas = new List<ProcessMetaPersist>
                        {
                            new()
                            {
                                Name = "LegacyStep",
                                FlowTemplate = "LegacyFlow",
                                ProcessTypeFullName = typeof(BlackProcess).FullName!,
                                IsEnabled = true,
                                ConfigJson = JsonConvert.SerializeObject(process.Config)
                            }
                        }
                    }
                }
            };
            string currentJson = JsonConvert.SerializeObject(
                legacyRoot,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All, Formatting = Formatting.Indented });
            JObject legacyJson = JObject.Parse(currentJson);
            foreach (JProperty property in legacyJson
                .DescendantsAndSelf()
                .OfType<JObject>()
                .Select(value => value.Property(nameof(ProcessMetaPersist.FlowCameraParameterOverrideConfig)))
                .Where(property => property != null)
                .Cast<JProperty>()
                .ToArray())
            {
                property.Remove();
            }
            File.WriteAllText(filePath, legacyJson.ToString(Formatting.Indented));

            var reloaded = new ProcessManager();

            ProcessMeta restored = Assert.Single(Assert.Single(reloaded.ProcessGroups).ProcessMetas);
            Assert.NotNull(restored.FlowCameraParameterOverrideConfig);
            Assert.False(restored.FlowCameraParameterOverrideConfig.IsEnabled);
            Assert.Equal(100, restored.FlowCameraParameterOverrideConfig.ExposureTimeMs);
            Assert.Equal(string.Empty, restored.FlowCameraParameterOverrideConfig.CalibrationTemplateName);
        });
    }

    [Fact]
    public void ImportedGroupsDoNotReplaceMemoryOrDiskWhenPersistenceFails()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            var manager = new ProcessManager();
            ProcessGroup originalGroup = Assert.Single(manager.ProcessGroups);
            RecipeConfig originalRecipeConfig = manager.RecipeConfig;
            Assert.True(manager.TrySaveProcessGroups());
            string filePath = Path.Combine(ViewResultManager.DirectoryPath, ProcessManager.GroupPersistFileName);
            string originalJson = File.ReadAllText(filePath);
            var importedProcess = new BlackProcess();
            importedProcess.Config.RecipeConfig.FOFOContrast.Min = 999;
            var importedRoot = new ProcessGroupsRoot
            {
                Version = 3,
                Groups = new List<ProcessGroupPersist>
                {
                    new()
                    {
                        Name = "Imported",
                        Metas = new List<ProcessMetaPersist>
                        {
                            new()
                            {
                                Name = "Imported",
                                FlowTemplate = "ImportedTemplate",
                                ProcessTypeFullName = typeof(BlackProcess).FullName!,
                                ConfigJson = JsonConvert.SerializeObject(importedProcess.Config)
                            }
                        }
                    }
                }
            };

            using (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.Throws<IOException>(() => manager.ApplyImportedGroups(importedRoot, new RecipeConfig()));
            }

            Assert.Same(originalGroup, Assert.Single(manager.ProcessGroups));
            Assert.Same(originalRecipeConfig, manager.RecipeConfig);
            Assert.Empty(manager.ResultParserMetas);
            Assert.Equal(originalJson, File.ReadAllText(filePath));
        });
    }

    [Fact]
    public void LegacySharedArvrGroupsMigrateWithoutChangingTheSharedFile()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            string sharedFilePath = Path.Combine(ViewResultManager.DirectoryPath, "ProcessGroups.json");
            var process = new BlackProcess();
            var legacyRoot = new ProcessGroupsRoot
            {
                Version = 3,
                Groups =
                [
                    new ProcessGroupPersist
                    {
                        Name = "ARVR Legacy",
                        Metas =
                        [
                            new ProcessMetaPersist
                            {
                                Name = "Black",
                                FlowTemplate = "BlackFlow",
                                ProcessTypeFullName = typeof(BlackProcess).FullName!,
                                ConfigJson = JsonConvert.SerializeObject(process.Config)
                            }
                        ]
                    }
                ]
            };
            string sharedJson = JsonConvert.SerializeObject(
                legacyRoot,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All, Formatting = Formatting.Indented });
            File.WriteAllText(sharedFilePath, sharedJson);

            var manager = new ProcessManager();

            Assert.Equal("ARVR Legacy", Assert.Single(manager.ProcessGroups).Name);
            Assert.IsType<BlackProcess>(Assert.Single(manager.ProcessMetas).Process);
            Assert.True(File.Exists(Path.Combine(ViewResultManager.DirectoryPath, ProcessManager.GroupPersistFileName)));
            Assert.Equal(sharedJson, File.ReadAllText(sharedFilePath));
        });
    }

    [Fact]
    public void SharedLuxGroupsAreIgnoredAndFutureSavesUseTheArvrFile()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            string sharedFilePath = Path.Combine(ViewResultManager.DirectoryPath, "ProcessGroups.json");
            const string sharedJson = """
                {
                  "Version": 1,
                  "ActiveGroupIndex": 0,
                  "Groups": [
                    {
                      "Name": "LUX",
                      "Metas": [
                        {
                          "Name": "White255",
                          "FlowTemplate": "LuxFlow",
                          "ProcessTypeFullName": "ProjectLUX.Process.W255.White255Process",
                          "IsEnabled": true,
                          "SocketCode": "21",
                          "ConfigJson": null
                        }
                      ]
                    }
                  ]
                }
                """;
            File.WriteAllText(sharedFilePath, sharedJson);

            var manager = new ProcessManager();

            Assert.Equal("Default", Assert.Single(manager.ProcessGroups).Name);
            Assert.Empty(manager.ProcessMetas);
            Assert.False(File.Exists(Path.Combine(ViewResultManager.DirectoryPath, ProcessManager.GroupPersistFileName)));

            Assert.True(manager.TryAddGroup("ARVR", out string error), error);

            Assert.True(File.Exists(Path.Combine(ViewResultManager.DirectoryPath, ProcessManager.GroupPersistFileName)));
            Assert.Equal(sharedJson, File.ReadAllText(sharedFilePath));
        });
    }

    [Fact]
    public void GroupNameChangesPreserveStepsAndRejectDuplicateOrInvalidNames()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            var manager = new ProcessManager();
            ProcessGroup original = Assert.Single(manager.ProcessGroups);
            var process = new BlackProcess();
            process.Config.RecipeConfig.FOFOContrast.Min = 123;
            var meta = CreateMeta("Step", process);
            original.ProcessMetas.Add(meta);
            Assert.True(manager.TryAddGroup("  Product A  ", out string error), error);
            Assert.Equal("Product A", manager.ActiveGroup!.Name);
            Assert.True(manager.AddGroupCommand.CanExecute(null));
            Assert.True(manager.RenameGroupCommand.CanExecute(null));

            string filePath = ProcessManager.GroupPersistFilePath;
            string jsonBeforeInvalidNames = File.ReadAllText(filePath);
            Assert.False(manager.TryAddGroup(" product a ", out error));
            Assert.False(manager.TryAddGroup("   ", out error));
            Assert.False(manager.TryRenameGroup(original, "PRODUCT A", out error));
            Assert.False(manager.TryRenameGroup(original, "Product\nB", out error));
            Assert.Equal(jsonBeforeInvalidNames, File.ReadAllText(filePath));
            Assert.Equal(2, manager.ProcessGroups.Count);
            Assert.Equal("Default", original.Name);

            Assert.True(manager.TryRenameGroup(original, "  Product B  ", out error), error);
            Assert.Same(meta, Assert.Single(original.ProcessMetas));
            Assert.Same(process, meta.Process);
            Assert.Equal("Product A", manager.ActiveGroup.Name);
            var reloaded = new ProcessManager();
            Assert.Equal("Product B", reloaded.ProcessGroups[0].Name);
            Assert.Equal("Product A", reloaded.ActiveGroup!.Name);
            var reloadedProcess = Assert.IsType<BlackProcess>(Assert.Single(reloaded.ProcessGroups[0].ProcessMetas).Process);
            Assert.Equal(123, reloadedProcess.Config.RecipeConfig.FOFOContrast.Min);
        });
    }

    [Fact]
    public void GroupNameSaveFailureKeepsMemoryAndDiskAndAllowsRetry()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            var manager = new ProcessManager();
            ProcessGroup original = Assert.Single(manager.ProcessGroups);
            Assert.True(manager.TrySaveProcessGroups());
            string filePath = ProcessManager.GroupPersistFilePath;
            string originalJson = File.ReadAllText(filePath);
            using (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.False(manager.TryAddGroup("Product A", out string addError));
                Assert.NotEmpty(addError);
                Assert.False(manager.TryRenameGroup(original, "Product B", out string renameError));
                Assert.NotEmpty(renameError);
                Assert.Same(original, Assert.Single(manager.ProcessGroups));
                Assert.Same(original, manager.ActiveGroup);
                Assert.Equal("Default", original.Name);
            }
            Assert.Equal(originalJson, File.ReadAllText(filePath));
            Assert.True(manager.TryAddGroup("Product A", out string error), error);
            Assert.True(manager.TryRenameGroup(original, "Product B", out error), error);
            Assert.Equal("Product B", new ProcessManager().ProcessGroups[0].Name);
        });
    }

    [Fact]
    public void LegacyRecipeImportUpdatesEveryGroupAndResultParserInstance()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            var manager = new ProcessManager();
            var first = new BlackProcess();
            var second = new BlackProcess();
            var parser = new BlackProcess();
            manager.ProcessGroups[0].ProcessMetas.Add(CreateMeta("First", first));
            var secondGroup = new ProcessGroup { Name = "Second" };
            secondGroup.ProcessMetas.Add(CreateMeta("Second", second));
            manager.ProcessGroups.Add(secondGroup);
            manager.ResultParserMetas.Add(CreateMeta("Parser", parser));
            var imported = new BlackRecipeConfig();
            imported.FOFOContrast.Min = 333;
            var importResult = new LegacyRecipeImportResult();
            importResult.SharedConfigs[typeof(BlackRecipeConfig)] = imported;

            var summary = manager.ApplyLegacyRecipe(importResult);

            Assert.Equal(3, summary.UpdatedProcessRecipes);
            Assert.Equal(333, first.Config.RecipeConfig.FOFOContrast.Min);
            Assert.Equal(333, second.Config.RecipeConfig.FOFOContrast.Min);
            Assert.Equal(333, parser.Config.RecipeConfig.FOFOContrast.Min);
            Assert.NotSame(first.Config.RecipeConfig, second.Config.RecipeConfig);
            Assert.NotSame(first.Config.RecipeConfig, parser.Config.RecipeConfig);
            Assert.NotSame(first.Config.RecipeConfig.FOFOContrast, second.Config.RecipeConfig.FOFOContrast);
        });
    }

    private static ProcessMeta CreateMeta(string name, BlackProcess process)
    {
        return new ProcessMeta
        {
            Name = name,
            FlowTemplate = name,
            Process = process,
            ConfigJson = JsonConvert.SerializeObject(process.Config)
        };
    }

    private static void RunInTemporaryPersistenceDirectory(Action action)
    {
        string originalDirectory = ViewResultManager.DirectoryPath;
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            ViewResultManager.DirectoryPath = temporaryDirectory + Path.DirectorySeparatorChar;
            action();
        }
        finally
        {
            ViewResultManager.DirectoryPath = originalDirectory;
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
