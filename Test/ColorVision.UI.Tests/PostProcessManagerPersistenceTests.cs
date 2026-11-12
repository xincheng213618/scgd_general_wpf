using ColorVision.Common.MVVM;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class PostProcessManagerPersistenceTests
{
    [Fact]
    public async Task RequiredPostProcessFollowsTheSavedTemplateAfterRename()
    {
        await WpfTestHost.Invoke(async () =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-PostProcessRename-");
            string path = Path.Combine(directory.FullName, "PostProcessConfig.json");
            var template = new TemplateModel<FlowParam>("Inspection", new FlowParam { FlowKey = "flow:fixture-original" });
            var replacement = new TemplateModel<FlowParam>("INSPECTION", new FlowParam { FlowKey = "flow:fixture-replacement" });
            FieldInfo managerInstance = typeof(PostProcessManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            object? previousManager = managerInstance.GetValue(null);
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(new[]
                {
                    new PostProcessPersist
                    {
                        Name = "Required archive", TemplateName = template.Key,
                        ProcessTypeFullName = "ColorVision.Tests.MissingArchive", FailurePolicy = PostProcessFailurePolicy.Required
                    }
                }));
                var manager = new PostProcessManager(path);
                managerInstance.SetValue(null, manager);
                FlowRunFinalizedData beforeRename = await Finalize(template);
                Assert.Equal(FlowFinalOutcome.Failed, beforeRename.FinalOutcome);

                TemplateFlow.Params.Add(template);
                TemplateFlow.Params.Add(replacement);
                Assert.Throws<InvalidOperationException>(() => PostProcessManager.BindLegacyTemplatesBeforeRename(template));
                Assert.Null(Assert.Single(manager.ProcessMetas).FlowKey);
                FlowRunFinalizedData ambiguous = await Finalize(template);
                Assert.Equal(FlowFinalOutcome.Failed, ambiguous.FinalOutcome);
                Assert.Equal(EngineLocalization.Get("流程名称重复，请编辑后处理项并重新选择绑定模板。"),
                    Assert.Single(ambiguous.PostProcessResults).Message);
                TemplateFlow.Params.Remove(replacement);

                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Exception? blocked = Record.Exception(() => PostProcessManager.BindLegacyTemplatesBeforeRename(template));
                    Assert.True(blocked is IOException or UnauthorizedAccessException, blocked?.ToString());
                    Assert.Null(Assert.Single(manager.ProcessMetas).FlowKey);
                    Assert.Null(ReadBinding().FlowKey);
                }
                PostProcessManager.BindLegacyTemplatesBeforeRename(template);
                Assert.Equal(template.Value.FlowKey, ReadBinding().FlowKey);

                template.Key = "Renamed inspection";
                TemplateFlow.Params.Add(replacement);
                managerInstance.SetValue(null, new PostProcessManager(path));
                FlowRunFinalizedData afterRename = await Finalize(template);
                Assert.Equal(FlowFinalOutcome.Failed, afterRename.FinalOutcome);
                Assert.Equal("Required archive", Assert.Single(afterRename.PostProcessResults).Name);

                TemplateFlow.Params.Remove(template);
                FlowRunFinalizedData otherTemplate = await Finalize(replacement);
                Assert.Equal(FlowFinalOutcome.Succeeded, otherTemplate.FinalOutcome);
                Assert.Empty(otherTemplate.PostProcessResults);
            }
            finally
            {
                managerInstance.SetValue(null, previousManager);
                TemplateFlow.Params.Remove(template);
                TemplateFlow.Params.Remove(replacement);
                File.Delete(path);
                directory.Delete();
            }

            PostProcessPersist ReadBinding() => Assert.Single(JsonConvert.DeserializeObject<PostProcessPersist[]>(File.ReadAllText(path))!);
        });

        static Task<FlowRunFinalizedData> Finalize(TemplateModel<FlowParam> flow)
            => new FlowRunFinalizer(new DefaultFlowPostProcessExecutor(), new NoPersistence()).FinalizeAsync(new FlowRunFinalizationRequest(
                new FlowControlData { Status = StatusTypeEnum.Completed }, new MeasureBatchModel(), flow.Key, 0) { FlowKey = flow.Value.FlowKey },
                journalScope: null);
    }

    private sealed class NoPersistence : IFlowRunFinalizationPersistence
    {
        public void ApplyRequiredPostProcessFailure(MeasureBatchModel batch, IReadOnlyList<PostProcessExecutionResult> results) { }
        public void RecordFallbackRun(MeasureBatchModel batch, string flowName, FlowControlData result, FlowStatus status, long elapsedMilliseconds) { }
    }

    [Theory]
    [InlineData(PostProcessFailurePolicy.Required, FlowFinalOutcome.Failed)]
    [InlineData(PostProcessFailurePolicy.Warning, FlowFinalOutcome.SucceededWithWarnings)]
    public void UnreadableConfigurationPreservesOrderAndItsFailurePolicy(
        PostProcessFailurePolicy policy, FlowFinalOutcome expectedOutcome)
    {
        WpfTestHost.Invoke(() =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-PostProcess-");
            string path = Path.Combine(directory.FullName, "PostProcessConfig.json");
            string processorType = typeof(TrackingPostProcessor).FullName!;
            const string missingType = "ColorVision.Tests.MissingPostProcessor";
            const string invalidJson = """{"Value":7,"Destination":{}}""";
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(new[]
                {
                    new PostProcessPersist
                    {
                        Name = "Unreadable archive", TemplateName = "Flow", ProcessTypeFullName = processorType,
                        ConfigJson = invalidJson, Tag = "Preserve", FailurePolicy = policy
                    },
                    new PostProcessPersist
                    {
                        Name = "Unavailable module", TemplateName = "Other flow", ProcessTypeFullName = missingType,
                        ConfigJson = "{}", FailurePolicy = PostProcessFailurePolicy.Warning
                    },
                    new PostProcessPersist
                    {
                        Name = "Later report", TemplateName = "Flow", ProcessTypeFullName = processorType,
                        ConfigJson = """{"Value":9}""", FailurePolicy = PostProcessFailurePolicy.Required
                    }
                }));

                var manager = new PostProcessManager(path);
                Assert.Equal(new[] { "Unreadable archive", "Unavailable module", "Later report" },
                    manager.ProcessMetas.Select(meta => meta.Name));
                Assert.Null(manager.ProcessMetas[0].PostProcessor);
                Assert.True(manager.ProcessMetas[0].HasUnreadableConfiguration);
                Assert.Null(manager.ProcessMetas[1].PostProcessor);
                Assert.False(manager.ProcessMetas[1].HasUnreadableConfiguration);
                var later = Assert.IsType<TrackingPostProcessor>(manager.ProcessMetas[2].PostProcessor);
                Assert.Equal(9, later.Config.Value);

                var results = PostProcessExecutionRunner.Execute(
                    manager.ProcessMetas.Where(meta => meta.TemplateName == "Flow"),
                    new PostProcessContext(new PostProcessConfig()) { FlowName = "Flow" });
                Assert.Collection(results,
                    unavailable =>
                    {
                        Assert.Equal("Unreadable archive", unavailable.Name);
                        Assert.Equal(PostProcessExecutionStatus.Unavailable, unavailable.Status);
                        Assert.Equal(processorType, unavailable.ProcessTypeName);
                        Assert.Equal(policy, unavailable.FailurePolicy);
                    },
                    succeeded =>
                    {
                        Assert.Equal("Later report", succeeded.Name);
                        Assert.True(succeeded.Succeeded);
                    });
                Assert.Equal(1, later.Executions);
                Assert.Equal(expectedOutcome, FlowFinalOutcomeResolver.Resolve(
                    new FlowControlData { Status = StatusTypeEnum.Completed }, results));

                manager.ProcessMetas.Add(new PostProcessMeta { Name = "New step", TemplateName = "Flow" });
                List<PostProcessPersist> saved = JsonConvert.DeserializeObject<List<PostProcessPersist>>(File.ReadAllText(path))!;
                Assert.Equal(4, saved.Count);
                Assert.Equal(processorType, saved[0].ProcessTypeFullName);
                Assert.Equal(invalidJson, saved[0].ConfigJson);
                Assert.Equal("Preserve", saved[0].Tag);
                Assert.Equal(policy, saved[0].FailurePolicy);
                Assert.Equal(missingType, saved[1].ProcessTypeFullName);

                var prototype = Assert.IsType<TrackingPostProcessor>(Assert.Single(manager.Processes,
                    process => process.GetType().FullName == manager.ProcessMetas[0].ProcessTypeFullName));
                var edit = new PostProcessMetaEditViewModel(
                    [new TemplateModel<FlowParam>("Flow", new FlowParam())], manager.Processes,
                    saved[0].Name, saved[0].TemplateName, prototype, saved[0].Tag, saved[0].FailurePolicy, "Edit");
                var draft = Assert.IsType<TrackingPostProcessor>(edit.SelectedProcess);
                Assert.NotSame(prototype, draft);
                draft.Config.Value = 17;
                Assert.Equal(0, prototype.Config.Value);

                saved[0].ConfigJson = JsonConvert.SerializeObject(draft.Config);
                File.WriteAllText(path, JsonConvert.SerializeObject(saved));
                var recovered = new PostProcessManager(path);
                Assert.False(recovered.ProcessMetas[0].HasUnreadableConfiguration);
                Assert.Equal(17, Assert.IsType<TrackingPostProcessor>(recovered.ProcessMetas[0].PostProcessor).Config.Value);
            }
            finally
            {
                File.Delete(path);
                directory.Delete();
            }
        });
    }

    [Fact]
    public void AConfigurableSharedInstanceCannotBeMutatedByLoadOrRepair()
    {
        WpfTestHost.Invoke(() =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-PostProcessShared-");
            string path = Path.Combine(directory.FullName, "PostProcessConfig.json");
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(new[]
                {
                    new PostProcessPersist
                    {
                        Name = "Shared action", TemplateName = "Flow",
                        ProcessTypeFullName = typeof(SharedPostProcessor).FullName!, ConfigJson = """{"Value":99}"""
                    }
                }));
                var manager = new PostProcessManager(path);
                Assert.Null(Assert.Single(manager.ProcessMetas).PostProcessor);
                var prototype = Assert.Single(manager.Processes.OfType<SharedPostProcessor>());

                var edit = new PostProcessMetaEditViewModel(
                    [new TemplateModel<FlowParam>("Flow", new FlowParam())], manager.Processes,
                    "Shared action", "Flow", prototype, string.Empty, PostProcessFailurePolicy.Warning, "Edit");

                Assert.Null(edit.SelectedProcess);
                Assert.Equal(0, prototype.Config.Value);
            }
            finally
            {
                File.Delete(path);
                directory.Delete();
            }
        });
    }

    public sealed class TrackingPostProcessor : IPostProcessor
    {
        public TrackingConfig Config { get; } = new();
        public int Executions { get; private set; }

        public object GetConfig() => Config;

        public void SetConfig(string configJson) => JsonConvert.PopulateObject(configJson, Config);

        public bool Process(PostProcessContext ctx)
        {
            Executions++;
            return true;
        }
    }

    public sealed class TrackingConfig : ViewModelBase
    {
        public int Value { get; set; }
        public string? Destination { get; set; }
    }

    public sealed class SharedPostProcessor : IPostProcessor
    {
        public TrackingConfig Config { get; } = new();

        public object GetConfig() => Config;
        public void SetConfig(string configJson) => JsonConvert.PopulateObject(configJson, Config);
        public IPostProcessor CreateInstance() => this;
        public bool Process(PostProcessContext ctx) => true;
    }
}
