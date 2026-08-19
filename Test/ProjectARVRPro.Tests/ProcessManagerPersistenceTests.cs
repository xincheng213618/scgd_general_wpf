using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Recipe;
using System.IO;
using Xunit;

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
                Process = sourceProcess,
                IsEnabled = false,
                ConfigJson = JsonConvert.SerializeObject(sourceProcess.Config),
                PictureSwitchConfig = new PictureSwitchConfig
                {
                    IsEnabled = true,
                    SendCommand = "PIC9",
                    SuccessDelayMs = 900
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
            Assert.Equal(source.IsEnabled, copy.IsEnabled);
            Assert.NotSame(source.Process, copy.Process);
            BlackProcess copiedProcess = Assert.IsType<BlackProcess>(copy.Process);
            Assert.Equal(123, copiedProcess.Config.RecipeConfig.FOFOContrast.Min);
            Assert.NotSame(sourceProcess.Config.RecipeConfig, copiedProcess.Config.RecipeConfig);
            Assert.NotSame(source.PictureSwitchConfig, copy.PictureSwitchConfig);
            Assert.True(copy.PictureSwitchConfig.IsEnabled);
            Assert.Equal("PIC9", copy.PictureSwitchConfig.SendCommand);
            Assert.Equal(900, copy.PictureSwitchConfig.SuccessDelayMs);

            copiedProcess.Config.RecipeConfig.FOFOContrast.Min = 456;
            copy.PictureSwitchConfig.SendCommand = "PICA";
            Assert.Equal(123, sourceProcess.Config.RecipeConfig.FOFOContrast.Min);
            Assert.Equal("PIC9", source.PictureSwitchConfig.SendCommand);
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
                Process = source,
                ConfigJson = JsonConvert.SerializeObject(new BlackProcessConfig())
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
            BlackProcess restoredSource = Assert.IsType<BlackProcess>(Assert.Single(reloaded.ProcessGroups[0].ProcessMetas).Process);
            BlackProcess restoredCopy = Assert.IsType<BlackProcess>(Assert.Single(reloaded.ProcessGroups[1].ProcessMetas).Process);
            BlackProcess restoredParser = Assert.IsType<BlackProcess>(Assert.Single(reloaded.ResultParserMetas).Process);

            Assert.Equal(111, restoredSource.Config.RecipeConfig.FOFOContrast.Min);
            Assert.Equal(111, restoredCopy.Config.RecipeConfig.FOFOContrast.Min);
            Assert.Equal(222, restoredParser.Config.RecipeConfig.FOFOContrast.Min);
            Assert.NotSame(restoredSource.Config.RecipeConfig, restoredCopy.Config.RecipeConfig);
            Assert.NotSame(restoredSource.Config.RecipeConfig.FOFOContrast, restoredCopy.Config.RecipeConfig.FOFOContrast);
            Assert.NotSame(restoredSource.Config.RecipeConfig, restoredParser.Config.RecipeConfig);
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
            string filePath = Path.Combine(ViewResultManager.DirectoryPath, "ProcessGroups.json");
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
