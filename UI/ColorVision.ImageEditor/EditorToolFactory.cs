using ColorVision.Algorithms;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Tooling;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{   
    /// <summary>
    /// 编辑器工具工厂 - 负责发现、创建和初始化编辑器工具
    /// </summary>
    public class IEditorToolFactory : IDisposable
    {
        private static readonly Type[] SupportedContextTypes =
        {
            typeof(EditorContext),
            typeof(DrawEditorContext),
            typeof(ImageProcessingContext),
            typeof(DrawCanvas),
            typeof(TextEditingContext),
            typeof(ImageViewConfig),
        };

        private readonly EditorToolbarComposer _toolbarComposer;
        private readonly EditorContext _context;
        private readonly List<IEditorTool> _imageOpenEditorTools = new();
        private IImageOpen? _currentImageOpen;

        public T? GetIEditorTool<T>() where T : IEditorTool => GetEffectiveEditorTools().OfType<T>().FirstOrDefault();

        public IEditorTool? GetIEditorTool(string guidId)
        {
            if (string.IsNullOrWhiteSpace(guidId))
            {
                return null;
            }

            return GetEffectiveEditorTools().FirstOrDefault(tool => string.Equals(tool.GuidId, guidId, StringComparison.Ordinal));
        }

        public ObservableCollection<IEditorTool> IEditorTools { get; set; } = new ObservableCollection<IEditorTool>();
        public ObservableCollection<IIEditorToolContextMenu> IIEditorToolContextMenus { get; set; } = new ObservableCollection<IIEditorToolContextMenu>();
        public ObservableCollection<IImageComponent> IImageComponents { get; set; } = new ObservableCollection<IImageComponent>();
        public Dictionary<string, IImageOpen> IImageOpens { get; set; } = new Dictionary<string, IImageOpen>();
        public ObservableCollection<IDVContextMenu> ContextMenuProviders { get; set; } = new ObservableCollection<IDVContextMenu>();


        public IEditorToolFactory(ImageView imageView, EditorContext context)
        {
            _toolbarComposer = new EditorToolbarComposer(imageView.GetRegionToolBar);
            _context = context;

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in AssemblyHandler.GetInstance().GetTypes(assembly))
                {
                    if (typeof(IDVContextMenu).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (CreateContextBoundOrDefaultInstance(type, context) is IDVContextMenu instance)
                        {
                            if (!IsAlgorithmMenuExecutable(instance, context)) continue;
                            ContextMenuProviders.Add(instance);
                        }
                    }
                }
            }

            // 加载上下文菜单
            foreach (var assembly in Application.Current.GetAssemblies())
            {
                foreach (var type in AssemblyHandler.GetInstance().GetTypes(assembly))
                {
                    if (typeof(IIEditorToolContextMenu).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (CreateContextBoundInstance(type, context) is IIEditorToolContextMenu instance)
                        {
                            if (!IsAlgorithmMenuExecutable(instance, context)) continue;
                            IIEditorToolContextMenus.Add(instance);
                        }
                    }
                }
            }

            // 加载编辑器工具
            foreach (var assembly in Application.Current.GetAssemblies())
            {
                foreach (var type in AssemblyHandler.GetInstance().GetTypes(assembly))
                {
                    if (typeof(IEditorTool).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract && CanCreateGlobalEditorTool(type))
                    {
                        if (CreateEditorTool(type, context) is IEditorTool instance)
                        {
                            IEditorTools.Add(instance);
                        }
                    }
                }
            }

            // 加载图像组件
            foreach (var item in AssemblyService.Instance.LoadImplementations<IImageComponent>())
            {
                IImageComponents.Add(item);
            }

            // 加载图像打开器
            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in AssemblyHandler.GetInstance().GetTypes(assembly))
                {
                    if (typeof(IImageOpen).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        var attr = type.GetCustomAttributes(typeof(FileExtensionAttribute), false)
                            .Cast<FileExtensionAttribute>().FirstOrDefault();
                        if (attr != null)
                        {
                            foreach (var ext in attr.Extensions)
                            {
                                var extLower = ext.ToLowerInvariant();

                                if (Activator.CreateInstance(type, context) is IImageOpen instance)
                                {
                                    IImageOpens.Add(extLower, instance);
                                }
                            }
                        }
                    }
                }
            }

            RefreshToolBars();
        }

        public IReadOnlyList<IEditorTool> GetEffectiveEditorTools()
        {
            List<IEditorTool> effectiveTools = new();
            HashSet<string> overriddenGuids = new(StringComparer.Ordinal);

            foreach (IEditorTool tool in _imageOpenEditorTools)
            {
                effectiveTools.Add(tool);
                if (!string.IsNullOrWhiteSpace(tool.GuidId))
                {
                    overriddenGuids.Add(tool.GuidId);
                }
            }

            foreach (IEditorTool tool in IEditorTools)
            {
                if (!string.IsNullOrWhiteSpace(tool.GuidId) && overriddenGuids.Contains(tool.GuidId))
                {
                    continue;
                }

                effectiveTools.Add(tool);
            }

            return effectiveTools;
        }

        public void ApplyImageOpenTools(IImageOpen? imageOpen)
        {
            if (_currentImageOpen is IImageOpenEditorToolLifecycle previousLifecycle)
            {
                previousLifecycle.OnEditorToolsDeactivated(_context);
            }

            _currentImageOpen = imageOpen;
            _imageOpenEditorTools.Clear();

            if (imageOpen is IImageOpenEditorToolProvider provider)
            {
                foreach (IEditorTool tool in provider.GetEditorTools())
                {
                    if (tool != null)
                    {
                        _imageOpenEditorTools.Add(tool);
                    }
                }
            }

            RefreshToolBars();

            if (imageOpen is IImageOpenEditorToolLifecycle lifecycle)
            {
                lifecycle.OnEditorToolsActivated(_context);
            }
        }

        public void RefreshToolBars() => _toolbarComposer.Refresh(GetEffectiveEditorTools());

        public void Dispose()
        {
            if (_currentImageOpen is IImageOpenEditorToolLifecycle lifecycle)
            {
                lifecycle.OnEditorToolsDeactivated(_context);
            }

            _toolbarComposer.Clear();

            HashSet<IDisposable> disposableTools = new();
            foreach (IDisposable item in IEditorTools.Concat(_imageOpenEditorTools).OfType<IDisposable>())
            {
                if (disposableTools.Add(item))
                {
                    item.Dispose();
                }
            }

            _imageOpenEditorTools.Clear();
            GC.SuppressFinalize(this);
        }

        private static bool CanCreateGlobalEditorTool(Type type)
        {
            return SelectContextConstructor(type) != null;
        }

        private static bool IsAlgorithmMenuExecutable(object instance, EditorContext context)
        {
            if (instance is not IAlgorithmCatalogBoundMenu algorithmMenu) return true;
            AlgorithmRuntime runtime = context.ProcessingContext.AlgorithmRuntime;
            return runtime.Catalog.TryResolve(algorithmMenu.AlgorithmId, out AlgorithmDescriptor? descriptor)
                && descriptor != null
                && StandardAlgorithmAdapterContract.IsCompatible(descriptor)
                && StandardAlgorithmAdapterContract.TryGetInteractiveRequiredCapabilities(
                    descriptor,
                    algorithmMenu.PlannedInputCount,
                    algorithmMenu.RequiresRoi,
                    algorithmMenu.RequiredCapabilities,
                    out AlgorithmHostCapabilities required)
                && runtime.CanExecuteDescriptor(descriptor, required);
        }

        private static object? CreateContextBoundInstance(Type type, EditorContext context)
        {
            ConstructorInfo? ctor = SelectContextConstructor(type);
            return ctor == null
                ? null
                : ctor.Invoke(ctor.GetParameters().Select(parameter => ResolveContextArgument(parameter.ParameterType, context)).ToArray());
        }

        private static object? CreateContextBoundOrDefaultInstance(Type type, EditorContext context)
        {
            return CreateContextBoundInstance(type, context)
                ?? (type.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(type) : null);
        }

        private static ConstructorInfo? SelectContextConstructor(Type type)
        {
            return type.GetConstructors()
                .Where(ctor =>
                {
                    ParameterInfo[] parameters = ctor.GetParameters();
                    return parameters.Length > 0 && parameters.All(parameter => CanResolveContextType(parameter.ParameterType));
                })
                .OrderByDescending(ctor => ctor.GetParameters().Length)
                .FirstOrDefault();
        }

        private static bool CanResolveContextType(Type contextType)
        {
            return SupportedContextTypes.Contains(contextType);
        }

        private static object ResolveContextArgument(Type contextType, EditorContext context)
        {
            if (contextType == typeof(DrawEditorContext))
            {
                return context.DrawEditorContext;
            }

            if (contextType == typeof(EditorContext))
            {
                return context;
            }

            if (contextType == typeof(ImageProcessingContext))
            {
                return context.ProcessingContext;
            }

            if (contextType == typeof(DrawCanvas))
            {
                return context.DrawEditorContext.DrawCanvas;
            }

            if (contextType == typeof(TextEditingContext))
            {
                return context.TextEditingContext;
            }

            if (contextType == typeof(ImageViewConfig))
            {
                return context.Config;
            }

            throw new InvalidOperationException($"Unsupported context type: {contextType.FullName}");
        }

        private static object? CreateEditorTool(Type type, EditorContext context)
        {
            return CreateContextBoundInstance(type, context);
        }

        /// <summary>
        /// 生成编辑器工具的 UI 元素
        /// </summary>
        /// <returns>Button 或 ToggleButton</returns>
        public static FrameworkElement GenIEditorTool(IEditorTool editorTool) => EditorToolbarComposer.CreateToolControl(editorTool);

        /// <summary>
        /// 尝试从资源中查找并返回图像
        /// </summary>
        public static Image TryFindResource(string resourcePath) => EditorToolbarComposer.CreateResourceImage(resourcePath);
    }
}
