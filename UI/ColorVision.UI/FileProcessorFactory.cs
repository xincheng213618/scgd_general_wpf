using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base.File;
using ColorVision.UI.HotKey;
using log4net;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI
{
    public class MenuFileOpen : MenuItemBase, IHotKey
    {
        public override int Order => 1;

        public override string Header => Properties.Resources.MenuFile + "...";

        public override string OwnerGuid => nameof(MenuOpen);

        public override string GuidId => nameof(MenuFileOpen);
        public override string InputGestureText => "Ctrl+O";
        public HotKeys HotKeys => new(FileHotkeyText.OpenFile, new Hotkey(Key.O, ModifierKeys.Control), Execute)
        {
            Description = FileHotkeyText.OpenFileDescription,
            Category = FileHotkeyText.Category
        };
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
                FileOpenRouteResult result;
                try
                {
                    result = await FileProcessorFactory.GetInstance().OpenFileAsync(selectedFilePath);
                }
                catch (Exception ex)
                {
                    result = new FileOpenRouteResult(true, false, ex.Message);
                }
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

        private static readonly ILog log = LogManager.GetLogger(typeof(FileProcessorFactory));

        private sealed record TypeMaps(
            Dictionary<string, Type[]> OpenActions,
            Dictionary<string, Type[]> Exporters,
            Type? GenericExporter);

        private volatile TypeMaps? _typeMaps;
        private readonly IAssemblyService _assemblyService;
        private readonly object _typeMapsLock = new();
        private int _typeMapsGeneration;

        /// <summary>
        /// Optional resource-aware handler installed by the solution module.
        /// Registered file actions remain available for standalone file mode.
        /// </summary>
        public Func<string, CancellationToken, Task<FileOpenRouteResult>>? ResourceOpenHandlerAsync { get; set; }

        private FileProcessorFactory() : this(AssemblyHandler.GetInstance()) { }

        internal FileProcessorFactory(IAssemblyService assemblyService)
        {
            _assemblyService = assemblyService;
            assemblyService.AssembliesChanged += (_, _) =>
            {
                lock (_typeMapsLock)
                {
                    _typeMapsGeneration++;
                    _typeMaps = null;
                }
            };
        }

        private TypeMaps GetTypeMaps()
        {
            while (true)
            {
                int generation;
                lock (_typeMapsLock)
                {
                    if (_typeMaps is { } cached) return cached;
                    generation = _typeMapsGeneration;
                }

                // Discovery may itself load assemblies; never hold the publication lock while scanning.
                TypeMaps built = BuildTypeMaps();
                lock (_typeMapsLock)
                {
                    if (generation != _typeMapsGeneration) continue;
                    return _typeMaps ??= built;
                }
            }
        }

        private TypeMaps BuildTypeMaps()
        {
            var assemblyService = _assemblyService;
            var openActions = new Dictionary<string, List<Type>>();
            var exporters = new Dictionary<string, List<Type>>();
            Type? genericExporter = null;

            foreach (Type type in assemblyService.GetImplementationTypes(typeof(IFileOpenActionProcessor)))
                AddByExtension(openActions, type);

            foreach (Type type in assemblyService.GetImplementationTypes(typeof(IFileExporter)))
            {
                if (type.GetCustomAttribute<GenericFileAttribute>() != null)
                    genericExporter = type;
                else
                    AddByExtension(exporters, type);
            }

            return new TypeMaps(
                openActions.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
                exporters.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
                genericExporter);
        }

        private static void AddByExtension(Dictionary<string, List<Type>> map, Type type)
        {
            var extAttr = type.GetCustomAttribute<FileExtensionAttribute>();
            if (extAttr == null)
                return;

            foreach (string extKey in extAttr.Extensions
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(extension => extension.ToLowerInvariant())
                .Distinct())
            {
                if (!map.TryGetValue(extKey, out List<Type>? types))
                    map[extKey] = types = new List<Type>();
                types.Add(type);
            }
        }

        /// <summary>Creates each handler independently so one broken contribution cannot hide the others.</summary>
        private static IEnumerable<T> CreateHandlers<T>(IEnumerable<Type> types) where T : class
        {
            foreach (Type type in types)
            {
                T? handler = null;
                try
                {
                    handler = Activator.CreateInstance(type) as T;
                }
                catch (Exception ex)
                {
                    log.Warn($"Failed to create file handler '{type.FullName}'.", ex);
                }

                if (handler != null)
                    yield return handler;
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
            if (!GetTypeMaps().OpenActions.TryGetValue(extension, out Type[]? actionTypes))
                return FileOpenRouteResult.NotHandled;

            return SelectFileOpenAction(CreateHandlers<IFileOpenActionProcessor>(actionTypes), filePath);
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
            TypeMaps typeMaps = GetTypeMaps();
            Type[] exporterTypes = typeMaps.Exporters.TryGetValue(extension, out Type[]? registeredTypes) ? registeredTypes : [];

            try
            {
                FileExportResult result = SelectFileExporter(CreateHandlers<IFileExporter>(exporterTypes), filePath);
                if (result.Handled)
                    return result;

                if (typeMaps.GenericExporter != null
                    && CreateHandlers<IFileExporter>([typeMaps.GenericExporter]).FirstOrDefault() is IFileExporter fallback)
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
