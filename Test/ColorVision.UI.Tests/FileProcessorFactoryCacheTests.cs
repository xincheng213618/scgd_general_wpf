using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class FileProcessorFactoryCacheTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssemblyChangeDuringDiscoveryCannotRepublishOldFileHandlers(bool changeDuringExporterQuery)
    {
        var assemblies = new ChangingAssemblyService(changeDuringExporterQuery);
        var factory = new FileProcessorFactory(assemblies);
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cacheboundary");
        File.WriteAllText(path, "test");
        try
        {
            Assert.True(factory.TryOpenFileAction(path).Succeeded);
            Assert.True(factory.TryExportFile(path).Succeeded);
            int queryCount = assemblies.QueryCount;
            Assert.True(factory.TryOpenFileAction(path).Succeeded);
            Assert.True(factory.TryExportFile(path).Succeeded);
            Assert.Equal(queryCount, assemblies.QueryCount);

            assemblies.RemoveHandlers();
            Assert.False(factory.TryOpenFileAction(path).Handled);
            Assert.False(factory.TryExportFile(path).Succeeded);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class ChangingAssemblyService(bool changeDuringExporterQuery) : IAssemblyService
    {
        private bool _changed;
        private bool _hasHandlers;
        public int QueryCount { get; private set; }
        public event EventHandler? AssembliesChanged;

        public IReadOnlyList<Type> GetImplementationTypes(Type serviceType)
        {
            QueryCount++;
            Type[] snapshot = !_hasHandlers ? [] : serviceType == typeof(IFileOpenActionProcessor)
                ? [typeof(OpenAction)] : [typeof(Exporter)];
            Type trigger = changeDuringExporterQuery ? typeof(IFileExporter) : typeof(IFileOpenActionProcessor);
            if (!_changed && serviceType == trigger)
            {
                _changed = true;
                _hasHandlers = true;
                // Return the old query snapshot after synchronously delivering its invalidation.
                AssembliesChanged?.Invoke(this, EventArgs.Empty);
            }
            return snapshot;
        }

        public void RemoveHandlers()
        {
            _hasHandlers = false;
            AssembliesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RegisterAssembly(Assembly assembly) => throw new NotSupportedException();
        public Assembly[] GetAssemblies() => throw new NotSupportedException();
        public Assembly[] RefreshAssemblies() => throw new NotSupportedException();
        public List<T> LoadImplementations<T>(params object?[]? args) where T : class => throw new NotSupportedException();
    }

    [FileExtension(".cacheboundary")]
    public sealed class OpenAction : IFileOpenActionProcessor
    {
        public int Order => 0;
        public FileOpenRouteResult OpenFile(string filePath) => new(true, true);
    }

    [FileExtension(".cacheboundary")]
    public sealed class Exporter : IFileExporter
    {
        public int Order => 0;
        public FileExportResult Export(string filePath) => new(true, true);
    }
}
