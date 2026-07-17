using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base.File;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI
{
    public class MenuFileOpen : MenuItemBase
    {
        public override int Order => 1;

        public override string Header => Properties.Resources.MenuFile + "...";

        public override string OwnerGuid => nameof(MenuOpen);

        public override string GuidId => nameof(MenuFileOpen);
        public override object? Icon
        {
            get
            {
                TextBlock text = new()
                {
                    Text = "\uE8E5", // 使用Unicode字符
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                return text;
            }
        }
        public override async void Execute()
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = Properties.Resources.AllFiles;
            openFileDialog.FilterIndex = openFileDialog.Filter.Split('|').Length / 2; ;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedFilePath = openFileDialog.FileName;
                if (string.IsNullOrEmpty(selectedFilePath))
                {
                    return;
                }
                FileOpenRouteResult result = await FileProcessorFactory.GetInstance().OpenFileAsync(selectedFilePath);
                if (!result.Succeeded && !result.Canceled)
                {
                    MessageBox.Show(string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? Properties.Resources.UnsupportedFileFormat
                        : result.ErrorMessage);
                }
            }
        }
    }

    public class FileProcessorFactory
    {
        private static readonly Lazy<FileProcessorFactory> _instance = new(() => new FileProcessorFactory());
        public static FileProcessorFactory GetInstance() => _instance.Value;

        private readonly Dictionary<string, List<Type>> _openActionTypeMap = new();
        private readonly Dictionary<string, List<Type>> _exporterTypeMap = new();
        private readonly HashSet<Assembly> _registeredAssemblies = new();
        private readonly object _syncRoot = new();
        private Type? _genericExporterType;

        /// <summary>
        /// Optional resource-aware handler installed by the solution module.
        /// Registered file actions remain available for standalone file mode.
        /// </summary>
        public Func<string, CancellationToken, Task<FileOpenRouteResult>>? ResourceOpenHandlerAsync { get; set; }

        private FileProcessorFactory()
        {
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
            foreach (Assembly assembly in AssemblyHandler.GetInstance().GetAssemblies())
                RegisterAssembly(assembly);
        }

        private void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs e)
        {
            RegisterAssembly(e.LoadedAssembly);
        }

        private void RegisterAssembly(Assembly assembly)
        {
            lock (_syncRoot)
            {
                if (!_registeredAssemblies.Add(assembly))
                    return;

                foreach (Type type in GetLoadableTypes(assembly).Where(type =>
                    !type.IsAbstract
                    && !type.IsInterface
                    && (typeof(IFileOpenActionProcessor).IsAssignableFrom(type)
                        || typeof(IFileExporter).IsAssignableFrom(type))))
                {
                    bool isOpenAction = typeof(IFileOpenActionProcessor).IsAssignableFrom(type);
                    bool isExporter = typeof(IFileExporter).IsAssignableFrom(type);
                    if (isExporter && type.GetCustomAttribute<GenericFileAttribute>() != null)
                    {
                        _genericExporterType = type;
                        continue;
                    }
                    var extAttr = type.GetCustomAttribute<FileExtensionAttribute>();
                    if (extAttr != null)
                    {
                        foreach (string extKey in extAttr.Extensions
                            .Where(extension => !string.IsNullOrWhiteSpace(extension))
                            .Select(extension => extension.ToLowerInvariant())
                            .Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            if (isOpenAction)
                            {
                                if (!_openActionTypeMap.ContainsKey(extKey))
                                    _openActionTypeMap[extKey] = new List<Type>();
                                _openActionTypeMap[extKey].Add(type);
                            }
                            if (isExporter)
                            {
                                if (!_exporterTypeMap.ContainsKey(extKey))
                                    _exporterTypeMap[extKey] = new List<Type>();
                                _exporterTypeMap[extKey].Add(type);
                            }
                        }
                    }
                }
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.OfType<Type>();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        public async Task<FileOpenRouteResult> OpenFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                return new FileOpenRouteResult(true, false, $"文件不存在：{filePath}");

            FileOpenRouteResult? routedResult = ResourceOpenHandlerAsync == null
                ? null
                : await ResourceOpenHandlerAsync(filePath, cancellationToken);
            if (routedResult?.Handled == true)
                return routedResult;

            FileOpenRouteResult actionResult = TryOpenFileAction(filePath);
            if (actionResult.Handled)
                return actionResult;
            return new FileOpenRouteResult(true, false, Properties.Resources.UnsupportedFileFormat);
        }

        public FileOpenRouteResult TryOpenFileAction(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            Type[] actionTypes;
            lock (_syncRoot)
            {
                actionTypes = _openActionTypeMap.TryGetValue(extension, out List<Type>? registeredTypes)
                    ? registeredTypes.ToArray()
                    : [];
            }
            if (actionTypes.Length == 0)
                return FileOpenRouteResult.NotHandled;

            return SelectFileOpenAction(actionTypes
                .Select(type => Activator.CreateInstance(type))
                .OfType<IFileOpenActionProcessor>(), filePath);
        }

        internal static FileOpenRouteResult SelectFileOpenAction(
            IEnumerable<IFileOpenActionProcessor> processors,
            string filePath)
        {
            foreach (IFileOpenActionProcessor processor in processors
                .OrderByDescending(processor => processor.Order))
            {
                FileOpenRouteResult result = processor.OpenFile(filePath);
                if (result.Handled)
                    return result;
            }
            return FileOpenRouteResult.NotHandled;
        }

        public bool ExportFile(string filePath) => TryExportFile(filePath).Succeeded;

        public FileExportResult TryExportFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new FileExportResult(true, false, $"文件不存在：{filePath}");

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            Type[] exporterTypes;
            Type? genericExporterType;
            lock (_syncRoot)
            {
                exporterTypes = _exporterTypeMap.TryGetValue(extension, out List<Type>? registeredTypes)
                    ? registeredTypes.ToArray()
                    : [];
                genericExporterType = _genericExporterType;
            }

            try
            {
                FileExportResult result = SelectFileExporter(
                    exporterTypes
                        .Select(type => Activator.CreateInstance(type))
                        .OfType<IFileExporter>(),
                    filePath);
                if (result.Handled)
                    return result;

                if (genericExporterType != null
                    && Activator.CreateInstance(genericExporterType) is IFileExporter fallback)
                {
                    result = fallback.Export(filePath);
                    if (result.Handled)
                        return result;
                }
            }
            catch (Exception ex)
            {
                return new FileExportResult(true, false, $"导出文件失败：{ex.Message}");
            }

            return new FileExportResult(true, false, Properties.Resources.UnsupportedFileFormat);
        }

        internal static FileExportResult SelectFileExporter(
            IEnumerable<IFileExporter> exporters,
            string filePath)
        {
            foreach (IFileExporter exporter in exporters.OrderByDescending(exporter => exporter.Order))
            {
                FileExportResult result = exporter.Export(filePath);
                if (result.Handled)
                    return result;
            }
            return FileExportResult.NotHandled;
        }
    }
}
