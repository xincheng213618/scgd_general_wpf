using Newtonsoft.Json;
using ProjectLUX.Process;
using ProjectLUX.Process.W255;
using System.IO;
using Xunit;

namespace ProjectLUX.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessManagerPersistenceTestGroup
{
    public const string Name = "ProjectLUX process persistence";
}

[Collection(ProcessManagerPersistenceTestGroup.Name)]
public sealed class ProcessManagerPersistenceTests
{
    [Fact]
    public void LegacySharedLuxGroupsMigrateWithoutChangingTheSharedFile()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            string sharedFilePath = Path.Combine(ViewResultManager.DirectoryPath, "ProcessGroups.json");
            var legacyRoot = new ProcessGroupsRoot
            {
                Version = 1,
                Groups =
                [
                    new ProcessGroupPersist
                    {
                        Name = "LUX Legacy",
                        Metas =
                        [
                            new ProcessMetaPersist
                            {
                                Name = "White255",
                                FlowTemplate = "LuxFlow",
                                ProcessTypeFullName = typeof(White255Process).FullName!,
                                IsEnabled = true,
                                SocketCode = "21"
                            }
                        ]
                    }
                ]
            };
            string sharedJson = JsonConvert.SerializeObject(legacyRoot, Formatting.Indented);
            File.WriteAllText(sharedFilePath, sharedJson);

            var manager = new ProcessManager();

            Assert.Equal("LUX Legacy", Assert.Single(manager.ProcessGroups).Name);
            ProcessMeta restored = Assert.Single(manager.ProcessMetas);
            Assert.IsType<White255Process>(restored.Process);
            Assert.Equal("21", restored.SocketCode);
            Assert.True(File.Exists(Path.Combine(ViewResultManager.DirectoryPath, ProcessManager.GroupPersistFileName)));
            Assert.Equal(sharedJson, File.ReadAllText(sharedFilePath));
        });
    }

    [Fact]
    public void SharedArvrGroupsAreIgnoredAndFutureSavesUseTheLuxFile()
    {
        RunInTemporaryPersistenceDirectory(() =>
        {
            string sharedFilePath = Path.Combine(ViewResultManager.DirectoryPath, "ProcessGroups.json");
            const string sharedJson = """
                {
                  "$type": "ProjectARVRPro.Process.ProcessGroupsRoot, ProjectARVRPro",
                  "Version": 3,
                  "ActiveGroupIndex": 0,
                  "Groups": {
                    "$type": "System.Collections.Generic.List`1[[ProjectARVRPro.Process.ProcessGroupPersist, ProjectARVRPro]], System.Private.CoreLib",
                    "$values": []
                  },
                  "ResultParsers": {
                    "$type": "System.Collections.Generic.List`1[[ProjectARVRPro.Process.ProcessMetaPersist, ProjectARVRPro]], System.Private.CoreLib",
                    "$values": []
                  },
                  "RecipeConfig": null
                }
                """;
            File.WriteAllText(sharedFilePath, sharedJson);

            var manager = new ProcessManager();

            Assert.Equal("Default", Assert.Single(manager.ProcessGroups).Name);
            Assert.Empty(manager.ProcessMetas);
            Assert.False(File.Exists(Path.Combine(ViewResultManager.DirectoryPath, ProcessManager.GroupPersistFileName)));

            manager.NewGroupName = "LUX";
            manager.AddGroupCommand.Execute(null);

            Assert.True(File.Exists(Path.Combine(ViewResultManager.DirectoryPath, ProcessManager.GroupPersistFileName)));
            Assert.Equal(sharedJson, File.ReadAllText(sharedFilePath));
        });
    }

    private static void RunInTemporaryPersistenceDirectory(Action action)
    {
        string originalDirectory = ViewResultManager.DirectoryPath;
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"ProjectLUX.Tests.{Guid.NewGuid():N}");
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
