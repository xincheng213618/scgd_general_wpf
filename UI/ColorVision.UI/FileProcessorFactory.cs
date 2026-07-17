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
        public override void Execute()
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
                FileOpenRouteResult result = FileProcessorFactory.GetInstance().OpenFile(selectedFilePath);
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

        private readonly Dictionary<string, List<Type>> _extTypeMap = new();
        private readonly Dictionary<string, List<Type>> _openActionTypeMap = new();
        private readonly HashSet<Assembly> _registeredAssemblies = new();
        private readonly object _syncRoot = new();
        private Type? _genericType;

        /// <summary>
        /// Optional resource-aware handler installed by the solution module.
        /// The legacy processor path remains available for standalone file mode.
        /// </summary>
        public Func<string, FileOpenRouteResult>? ResourceOpenHandler { get; set; }

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
                    typeof(IFileProcessor).IsAssignableFrom(type)
                    && !type.IsAbstract
                    && !type.IsInterface))
                {
                    if (type.GetCustomAttribute<GenericFileAttribute>() != null)
                    {
                        _genericType = type;
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
                            if (!_extTypeMap.ContainsKey(extKey))
                                _extTypeMap[extKey] = new List<Type>();
                            _extTypeMap[extKey].Add(type);
                            if (typeof(IFileOpenActionProcessor).IsAssignableFrom(type))
                            {
                                if (!_openActionTypeMap.ContainsKey(extKey))
                                    _openActionTypeMap[extKey] = new List<Type>();
                                _openActionTypeMap[extKey].Add(type);
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

        public IFileProcessor? GetFileProcessor(
            string filePath,
            bool includeOpenActions = true)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            Type[] typeList;
            Type? genericType;
            lock (_syncRoot)
            {
                typeList = _extTypeMap.TryGetValue(extension, out List<Type>? registeredTypes)
                    ? registeredTypes.ToArray()
                    : [];
                genericType = _genericType;
            }

            var candidates = new List<IFileProcessor>();

            if (typeList.Length > 0)
            {
                foreach (Type type in typeList)
                {
                    if ((!includeOpenActions && typeof(IFileOpenActionProcessor).IsAssignableFrom(type))
                        || Activator.CreateInstance(type) is not IFileProcessor processor)
                        continue;
                    candidates.Add(processor);
                }
            }

            // 如果有候选，取 Order 最大的
            if (candidates.Count > 0)
                return candidates.OrderByDescending(p => p.Order).First();

            // 如果没有，尝试 fallback
            if (genericType != null
                && (includeOpenActions || !typeof(IFileOpenActionProcessor).IsAssignableFrom(genericType)))
            {
                if (Activator.CreateInstance(genericType) is IFileProcessor fallback)
                    return fallback;
            }
            return null;
        }

        public bool HandleFile(string filePath)
        {
            return OpenFile(filePath).Succeeded;
        }

        public FileOpenRouteResult OpenFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new FileOpenRouteResult(true, false, $"文件不存在：{filePath}");

            bool usedResourceRouter = ResourceOpenHandler != null;
            FileOpenRouteResult? routedResult = ResourceOpenHandler?.Invoke(filePath);
            if (routedResult?.Handled == true)
                return routedResult;

            var processor = GetFileProcessor(filePath, includeOpenActions: !usedResourceRouter);
            if (processor != null)
            {
                bool result = processor.Process(filePath);
                return new FileOpenRouteResult(
                    true,
                    result,
                    result ? string.Empty : Properties.Resources.UnsupportedFileFormat);
            }
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

        public bool ExportFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            Type[] typeList;
            Type? genericType;
            lock (_syncRoot)
            {
                typeList = _extTypeMap.TryGetValue(extension, out List<Type>? registeredTypes)
                    ? registeredTypes.ToArray()
                    : [];
                genericType = _genericType;
            }

            var candidates = new List<IFileProcessor>();

            if (typeList.Length > 0)
            {
                foreach (var t in typeList)
                {
                    if (Activator.CreateInstance(t) is IFileProcessor processor )
                    {
                        candidates.Add(processor);
                    }
                }
            }

            // 先取 Order 最大的能导出的
            if (candidates.Count > 0)
            {
                var best = candidates.OrderByDescending(p => p.Order).First();
                best.Export(filePath);
                return true;
            }

            // fallback
            if (genericType != null)
            {
                if (Activator.CreateInstance(genericType) is IFileProcessor fallback)
                {
                    fallback.Export(filePath);
                    return true;
                }
            }
            return false;
        }
    }
}
