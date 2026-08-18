using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.AOI;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.DemuraAOI;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Process.KeyedResults.FieldOfView;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.MTF;
using ProjectARVRPro.Process.MTF.MTFH;
using ProjectARVRPro.Process.MTF.MTFHV;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.MTF.MTFHV058;
using ProjectARVRPro.Process.MTF.MTFHVDynamic;
using ProjectARVRPro.Process.MTF.MTFV;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.W255;
using ProjectARVRPro.Process.W51;
using ProjectARVRPro.Recipe;
using System.IO;
using System.Reflection;
using Xunit;
using DynamicMTFH07Process = ProjectARVRPro.Process.MTF.MTF07.MTFH.MTFH07Process;
using DynamicMTFH07ProcessConfig = ProjectARVRPro.Process.MTF.MTF07.MTFH.MTFH07ProcessConfig;
using DynamicMTFH07RecipeConfig = ProjectARVRPro.Process.MTF.MTF07.MTFH.MTFH07RecipeConfig;
using DynamicMTFV07Process = ProjectARVRPro.Process.MTF.MTF07.MTFV.MTFV07Process;
using DynamicMTFV07ProcessConfig = ProjectARVRPro.Process.MTF.MTF07.MTFV.MTFV07ProcessConfig;
using DynamicMTFV07RecipeConfig = ProjectARVRPro.Process.MTF.MTF07.MTFV.MTFV07RecipeConfig;

namespace ProjectARVRPro.Tests;

public sealed class EmbeddedRecipeConfigTests
{
    public static TheoryData<Type, Type, Type> EmbeddedRecipeProcesses => new()
    {
        { typeof(BlackProcess), typeof(BlackProcessConfig), typeof(BlackRecipeConfig) },
        { typeof(ChessboardProcess), typeof(ChessboardProcessConfig), typeof(ChessboardRecipeConfig) },
        { typeof(ChessboardDynamicProcess), typeof(ChessboardDynamicProcessConfig), typeof(ChessboardRecipeConfig) },
        { typeof(DistortionProcess), typeof(DistortionProcessConfig), typeof(DistortionRecipeConfig) },
        { typeof(DistortionDynamicProcess), typeof(DistortionDynamicProcessConfig), typeof(DistortionRecipeConfig) },
        { typeof(OpticCenterProcess), typeof(OpticCenterProcessConfig), typeof(OpticCenterRecipeConfig) },
        { typeof(OpticCenterDynamicProcess), typeof(OpticCenterDynamicProcessConfig), typeof(OpticCenterRecipeConfig) },
        { typeof(DemuraAoiProcess), typeof(DemuraAoiProcessConfig), typeof(DemuraAoiRecipeConfig) },
        { typeof(White255Process), typeof(W255ProcessConfig), typeof(W255RecipeConfig) },
        { typeof(White51Process), typeof(W51ProcessConfig), typeof(W51RecipeConfig) },
        { typeof(MTFHProcess), typeof(MTFHProcessConfig), typeof(MTFHRecipeConfig) },
        { typeof(MTFHVProcess), typeof(MTFHVProcessConfig), typeof(MTFHVRecipeConfig) },
        { typeof(MTFHV048Process), typeof(MTFHV048ProcessConfig), typeof(MTFHV048RecipeConfig) },
        { typeof(MTFHV058Process), typeof(MTFHV058ProcessConfig), typeof(MTFHV058RecipeConfig) },
        { typeof(MTFVProcess), typeof(MTFVProcessConfig), typeof(MTFVRecipeConfig) },
        { typeof(DynamicMTFH07Process), typeof(DynamicMTFH07ProcessConfig), typeof(DynamicMTFH07RecipeConfig) },
        { typeof(DynamicMTFV07Process), typeof(DynamicMTFV07ProcessConfig), typeof(DynamicMTFV07RecipeConfig) }
    };

    public static TheoryData<Type, Type> ImportableRecipeProcesses => new()
    {
        { typeof(BlackProcess), typeof(BlackRecipeConfig) },
        { typeof(ChessboardProcess), typeof(ChessboardRecipeConfig) },
        { typeof(ChessboardDynamicProcess), typeof(ChessboardRecipeConfig) },
        { typeof(DistortionProcess), typeof(DistortionRecipeConfig) },
        { typeof(DistortionDynamicProcess), typeof(DistortionRecipeConfig) },
        { typeof(OpticCenterProcess), typeof(OpticCenterRecipeConfig) },
        { typeof(OpticCenterDynamicProcess), typeof(OpticCenterRecipeConfig) },
        { typeof(DemuraAoiProcess), typeof(DemuraAoiRecipeConfig) },
        { typeof(White255Process), typeof(W255RecipeConfig) },
        { typeof(White51Process), typeof(W51RecipeConfig) },
        { typeof(MTFProcess), typeof(MTFRecipeConfig) },
        { typeof(MTFHProcess), typeof(MTFHRecipeConfig) },
        { typeof(MTFHVProcess), typeof(MTFHVRecipeConfig) },
        { typeof(MTFHV048Process), typeof(MTFHV048RecipeConfig) },
        { typeof(MTFHV058Process), typeof(MTFHV058RecipeConfig) },
        { typeof(MTFHVDynamicProcess), typeof(MTFHVDynamicRecipeConfig) },
        { typeof(MTFVProcess), typeof(MTFVRecipeConfig) },
        { typeof(DynamicMTFH07Process), typeof(DynamicMTFH07RecipeConfig) },
        { typeof(DynamicMTFV07Process), typeof(DynamicMTFV07RecipeConfig) },
        { typeof(AOIProcess), typeof(AoiRecipeConfig) },
        { typeof(LuminanceChromaticityProcess), typeof(LuminanceChromaticityRecipeConfig) },
        { typeof(FieldOfViewProcess), typeof(FieldOfViewRecipeConfig) }
    };

    public static TheoryData<Type, Type> ExistingEmbeddedRecipeProcesses => new()
    {
        { typeof(MTFProcess), typeof(MTFRecipeConfig) },
        { typeof(MTFHVDynamicProcess), typeof(MTFHVDynamicRecipeConfig) },
        { typeof(AOIProcess), typeof(AoiRecipeConfig) },
        { typeof(LuminanceChromaticityProcess), typeof(LuminanceChromaticityRecipeConfig) },
        { typeof(FieldOfViewProcess), typeof(FieldOfViewRecipeConfig) }
    };

    [Theory]
    [MemberData(nameof(EmbeddedRecipeProcesses))]
    public void RecipeIsIndependentAndRoundTripsWithProcessConfig(
        Type processType,
        Type processConfigType,
        Type recipeConfigType)
    {
        IProcess first = CreateProcess(processType);
        IProcess second = CreateProcess(processType);

        object firstConfig = first.GetProcessConfig();
        object secondConfig = second.GetProcessConfig();
        object firstRecipe = GetRecipeConfig(first);
        object secondRecipe = GetRecipeConfig(second);
        Assert.IsType(processConfigType, firstConfig);
        Assert.IsType(processConfigType, secondConfig);
        Assert.IsType(recipeConfigType, firstRecipe);
        Assert.IsType(recipeConfigType, secondRecipe);
        PropertyInfo recipeProperty = Assert.Single(
            processConfigType.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => property.Name == "RecipeConfig");

        Assert.Same(recipeProperty.GetValue(firstConfig), firstRecipe);
        Assert.Same(recipeProperty.GetValue(secondConfig), secondRecipe);
        Assert.NotSame(firstConfig, secondConfig);
        Assert.NotSame(firstRecipe, secondRecipe);

        PropertyInfo recipeValueProperty = recipeConfigType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .First(property => typeof(RecipeBase).IsAssignableFrom(property.PropertyType));
        RecipeBase firstRecipeValue = Assert.IsAssignableFrom<RecipeBase>(recipeValueProperty.GetValue(firstRecipe));
        const double expectedMin = 1234.56789;
        firstRecipeValue.Min = expectedMin;

        string configJson = JsonConvert.SerializeObject(firstConfig);
        Assert.NotNull(JObject.Parse(configJson)["RecipeConfig"]);

        IProcess restored = CreateProcess(processType);
        restored.SetProcessConfig(configJson);

        object restoredConfig = restored.GetProcessConfig();
        object restoredRecipe = GetRecipeConfig(restored);
        Assert.IsType(processConfigType, restoredConfig);
        Assert.IsType(recipeConfigType, restoredRecipe);
        RecipeBase restoredRecipeValue = Assert.IsAssignableFrom<RecipeBase>(recipeValueProperty.GetValue(restoredRecipe));

        Assert.Same(recipeProperty.GetValue(restoredConfig), restoredRecipe);
        Assert.NotSame(firstRecipe, restoredRecipe);
        Assert.Equal(expectedMin, restoredRecipeValue.Min);

        IProcess missingRecipe = CreateProcess(processType);
        missingRecipe.SetProcessConfig("{}");
        Assert.IsType(recipeConfigType, missingRecipe.GetRecipeConfig());

        IProcess nullRecipe = CreateProcess(processType);
        nullRecipe.SetProcessConfig("{\"RecipeConfig\":null}");
        Assert.IsType(recipeConfigType, nullRecipe.GetRecipeConfig());
    }

    [Theory]
    [MemberData(nameof(ImportableRecipeProcesses))]
    public void LegacyRecipeImportCopiesValuesIntoEachMatchingProcessInstance(Type processType, Type recipeConfigType)
    {
        IProcess first = CreateProcess(processType);
        IProcess second = CreateProcess(processType);
        IRecipeConfig imported = Assert.IsAssignableFrom<IRecipeConfig>(Activator.CreateInstance(recipeConfigType));
        PropertyInfo recipeValueProperty = recipeConfigType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .First(property => typeof(RecipeBase).IsAssignableFrom(property.PropertyType));
        RecipeBase importedValue = Assert.IsAssignableFrom<RecipeBase>(recipeValueProperty.GetValue(imported));
        importedValue.Min = 321.45;
        var importedConfigs = new Dictionary<Type, IRecipeConfig>
        {
            [recipeConfigType] = imported
        };
        var firstMeta = new ProcessMeta { Process = first };
        var secondMeta = new ProcessMeta { Process = second };

        Assert.True(ProcessManager.ApplyImportedRecipe(firstMeta, importedConfigs));
        Assert.True(ProcessManager.ApplyImportedRecipe(secondMeta, importedConfigs));

        IRecipeConfig firstRecipe = Assert.IsAssignableFrom<IRecipeConfig>(first.GetRecipeConfig());
        IRecipeConfig secondRecipe = Assert.IsAssignableFrom<IRecipeConfig>(second.GetRecipeConfig());
        Assert.IsType(recipeConfigType, firstRecipe);
        Assert.IsType(recipeConfigType, secondRecipe);
        RecipeBase firstValue = Assert.IsAssignableFrom<RecipeBase>(recipeValueProperty.GetValue(firstRecipe));
        RecipeBase secondValue = Assert.IsAssignableFrom<RecipeBase>(recipeValueProperty.GetValue(secondRecipe));

        Assert.Equal(321.45, firstValue.Min);
        Assert.Equal(321.45, secondValue.Min);
        Assert.NotSame(imported, firstRecipe);
        Assert.NotSame(firstRecipe, secondRecipe);
        Assert.NotSame(importedValue, firstValue);
        Assert.NotSame(firstValue, secondValue);
        Assert.Equal(321.45, JObject.Parse(firstMeta.ConfigJson)["RecipeConfig"]?[recipeValueProperty.Name]?["Min"]?.Value<double>());
    }

    [Fact]
    public void LegacyRecipeImportKeepsExistingNestedDefaultsWhenLegacyValueIsNull()
    {
        var process = new BlackProcess();
        process.Config.RecipeConfig.FOFOContrast.Min = 12.34;
        BlackRecipeConfig imported = JsonConvert.DeserializeObject<BlackRecipeConfig>("{\"FOFOContrast\":null}")!;
        var meta = new ProcessMeta { Process = process };

        Assert.True(ProcessManager.ApplyImportedRecipe(meta, new Dictionary<Type, IRecipeConfig>
        {
            [typeof(BlackRecipeConfig)] = imported
        }));

        Assert.NotNull(process.Config.RecipeConfig.FOFOContrast);
        Assert.Equal(12.34, process.Config.RecipeConfig.FOFOContrast.Min);
    }

    [Theory]
    [MemberData(nameof(ExistingEmbeddedRecipeProcesses))]
    public void ExistingEmbeddedRecipeRemainsNonNullWhenConfigJsonContainsNull(Type processType, Type recipeConfigType)
    {
        IProcess process = CreateProcess(processType);

        process.SetProcessConfig("{\"RecipeConfig\":null}");

        Assert.IsType(recipeConfigType, process.GetRecipeConfig());
    }

    [Fact]
    public void LegacyRecipeContainerCreatesAndCachesARecipeWhenItsDictionaryIsMissing()
    {
        var container = new ProjectARVRPro.RecipeConfig { Configs = null! };

        BlackRecipeConfig first = container.GetRequiredService<BlackRecipeConfig>();
        BlackRecipeConfig second = container.GetRequiredService<BlackRecipeConfig>();

        Assert.Same(first, second);
        Assert.Same(first, container.Configs[typeof(BlackRecipeConfig)]);
    }

    [Fact]
    public void BuiltInProcessesDoNotUseTheSharedRecipeBaseClass()
    {
        Type[] sharedRecipeProcesses = typeof(IProcess).Assembly.GetTypes()
            .Where(type => typeof(IProcess).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => GetBaseTypes(type).Any(baseType =>
                baseType.IsGenericType
                && baseType.GetGenericTypeDefinition() == typeof(ProcessBase<,>)))
            .ToArray();

        Assert.Empty(sharedRecipeProcesses);
    }

    [Fact]
    public void AtomicWriteReplacesTheFileAndKeepsThePreviousVersionAsBackup()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "ProcessGroups.json");
        try
        {
            File.WriteAllText(filePath, "old");

            ProcessManager.WriteTextAtomically(filePath, "new");

            Assert.Equal("new", File.ReadAllText(filePath));
            Assert.Equal("old", File.ReadAllText(filePath + ".bak"));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AtomicWriteFailureLeavesTheExistingFileUnchanged()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "ProcessGroups.json");
        try
        {
            File.WriteAllText(filePath, "old");
            using (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() => ProcessManager.WriteTextAtomically(filePath, "new"));
            }

            Assert.Equal("old", File.ReadAllText(filePath));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ResultSnapshotRestoresTheRecipeUsedByThatResult()
    {
        var process = new BlackProcess();
        process.Config.RecipeConfig.FOFOContrast.Min = 456.78;
        var result = new ProjectARVRReuslt();

        ResultProcessResolver.Capture(result, process);
        process.Config.RecipeConfig.FOFOContrast.Min = -1;

        BlackProcess restored = Assert.IsType<BlackProcess>(ResultProcessResolver.Resolve(
            result,
            new IProcess[] { new BlackProcess() },
            Array.Empty<ProcessMeta>()));

        Assert.Equal(456.78, restored.Config.RecipeConfig.FOFOContrast.Min);
        Assert.NotSame(process.Config.RecipeConfig, restored.Config.RecipeConfig);
    }

    private static IProcess CreateProcess(Type processType)
    {
        return Assert.IsAssignableFrom<IProcess>(Activator.CreateInstance(processType));
    }

    private static object GetRecipeConfig(IProcess process)
    {
        object? recipeConfig = process.GetRecipeConfig();
        Assert.NotNull(recipeConfig);
        return recipeConfig;
    }

    private static IEnumerable<Type> GetBaseTypes(Type type)
    {
        for (Type? current = type.BaseType; current != null; current = current.BaseType)
            yield return current;
    }
}
