using ColorVision.Common.MVVM;
using ProjectARVRPro.Process;
using System.Windows.Documents;
using System.Windows.Media;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class ResultProcessResolverTests
    {
        [Fact]
        public void CaptureAndResolveRestoresTypeAndConfigurationWithoutCurrentMeta()
        {
            var source = new SnapshotProcess();
            source.Config.Label = "historical";
            var result = new ProjectARVRReuslt { Model = "template-a" };

            ResultProcessResolver.Capture(result, source);
            IProcess? restored = ResultProcessResolver.Resolve(result, [new SnapshotProcess()], []);

            Assert.Equal(typeof(SnapshotProcess).FullName, result.ProcessTypeFullName);
            SnapshotProcess process = Assert.IsType<SnapshotProcess>(restored);
            Assert.NotSame(source, process);
            Assert.Equal("historical", process.Config.Label);
        }

        [Fact]
        public void ResolvePrefersPersistedTypeOverCurrentTemplateMapping()
        {
            var result = new ProjectARVRReuslt { Model = "shared-template" };
            ResultProcessResolver.Capture(result, new SnapshotProcess());
            var currentMeta = new ProcessMeta
            {
                FlowTemplate = result.Model,
                Process = new OtherProcess()
            };

            IProcess? restored = ResultProcessResolver.Resolve(
                result,
                [new SnapshotProcess(), new OtherProcess()],
                [currentMeta]);

            Assert.IsType<SnapshotProcess>(restored);
        }

        [Fact]
        public void ResolveUsesCurrentTemplateForLegacyResult()
        {
            var legacyProcess = new OtherProcess();
            var result = new ProjectARVRReuslt { Model = "legacy-template" };
            var currentMeta = new ProcessMeta
            {
                FlowTemplate = result.Model,
                Process = legacyProcess
            };

            IProcess? restored = ResultProcessResolver.Resolve(result, [], [currentMeta]);

            Assert.Same(legacyProcess, restored);
        }

        public sealed class SnapshotProcessConfig : ViewModelBase
        {
            public string Label { get; set; } = string.Empty;
        }

        public sealed class SnapshotProcess : ProcessBase<SnapshotProcessConfig>
        {
            public override Task<bool> Execute(IProcessExecutionContext ctx) => Task.FromResult(true);
            public override void Render(IProcessExecutionContext ctx) { }
            public override void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize) { }
        }

        public sealed class OtherProcess : IProcess
        {
            public Task<bool> Execute(IProcessExecutionContext ctx) => Task.FromResult(true);
            public void Render(IProcessExecutionContext ctx) { }
            public void GenText(IProcessExecutionContext ctx, Paragraph paragraph, Brush foreground, double fontSize) { }
        }
    }
}
