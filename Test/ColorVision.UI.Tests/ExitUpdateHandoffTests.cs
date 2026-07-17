using ColorVision.Update;
using System;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests
{
    public sealed class ExitUpdateHandoffTests : IDisposable
    {
        private readonly string _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionUpdateHandoff-{Guid.NewGuid():N}");

        public ExitUpdateHandoffTests()
        {
            Directory.CreateDirectory(_rootDirectory);
        }

        [Fact]
        public void ActiveUpdateDefersLaunchAndRecordsReopenRequest()
        {
            (string programDirectory, string updateRoot, string stateRoot) = CreateActiveUpdate();
            ExitUpdateHandoffState state = ExitUpdateHandoff.Prepare(programDirectory, updateRoot, stateRoot);

            bool shouldDefer = ExitUpdateHandoff.TryDeferLaunchForActiveUpdate(
                programDirectory,
                launchToken: null,
                stateRootOverride: stateRoot);

            Assert.True(shouldDefer);
            Assert.True(File.Exists(state.MarkerPath));
            Assert.True(File.Exists(state.ReopenRequestPath));
        }

        [Fact]
        public void UpdaterAuthorizedLaunchClearsHandoffAndContinues()
        {
            (string programDirectory, string updateRoot, string stateRoot) = CreateActiveUpdate();
            ExitUpdateHandoffState state = ExitUpdateHandoff.Prepare(programDirectory, updateRoot, stateRoot);
            File.WriteAllText(state.ReopenRequestPath, "requested", new UTF8Encoding(false));

            bool shouldDefer = ExitUpdateHandoff.TryDeferLaunchForActiveUpdate(
                programDirectory,
                state.LaunchToken,
                stateRoot);

            Assert.False(shouldDefer);
            Assert.False(File.Exists(state.MarkerPath));
            Assert.False(File.Exists(state.ReopenRequestPath));
        }

        [Fact]
        public void MissingUpdateBatchClearsStaleHandoffAndContinues()
        {
            string programDirectory = Path.Combine(_rootDirectory, "ColorVision");
            string updateRoot = Path.Combine(_rootDirectory, "UpdateRoot");
            string stateRoot = Path.Combine(_rootDirectory, "State");
            Directory.CreateDirectory(programDirectory);
            Directory.CreateDirectory(updateRoot);
            ExitUpdateHandoffState state = ExitUpdateHandoff.Prepare(programDirectory, updateRoot, stateRoot);

            bool shouldDefer = ExitUpdateHandoff.TryDeferLaunchForActiveUpdate(
                programDirectory,
                launchToken: null,
                stateRootOverride: stateRoot);

            Assert.False(shouldDefer);
            Assert.False(File.Exists(state.MarkerPath));
        }

        [Fact]
        public void ExitedUpdaterProcessClearsHandoffAndContinues()
        {
            (string programDirectory, string updateRoot, string stateRoot) = CreateActiveUpdate();
            ExitUpdateHandoffState state = ExitUpdateHandoff.Prepare(programDirectory, updateRoot, stateRoot);
            Assert.True(ExitUpdateHandoff.TryActivate(state, int.MaxValue));

            bool shouldDefer = ExitUpdateHandoff.TryDeferLaunchForActiveUpdate(
                programDirectory,
                launchToken: null,
                stateRootOverride: stateRoot);

            Assert.False(shouldDefer);
            Assert.False(File.Exists(state.MarkerPath));
        }

        private (string ProgramDirectory, string UpdateRoot, string StateRoot) CreateActiveUpdate()
        {
            string programDirectory = Path.Combine(_rootDirectory, "ColorVision Install");
            string updateRoot = Path.Combine(_rootDirectory, "Update Root");
            string stateRoot = Path.Combine(_rootDirectory, "State");
            Directory.CreateDirectory(programDirectory);
            Directory.CreateDirectory(updateRoot);
            File.WriteAllText(Path.Combine(updateRoot, "update.bat"), "@echo off", new UTF8Encoding(false));
            return (programDirectory, updateRoot, stateRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
                Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
