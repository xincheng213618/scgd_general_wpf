using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Settings;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

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

        private readonly ImageView _imageView;
        private readonly EditorContext _context;
        private readonly List<IEditorTool> _imageOpenEditorTools = new();
        private readonly List<FrameworkElement> _generatedToolElements = new();
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
            _imageView = imageView;
            _context = context;

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IDVContextMenu).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (CreateContextBoundOrDefaultInstance(type, context) is IDVContextMenu instance)
                        {
                            ContextMenuProviders.Add(instance);
                        }
                    }
                }
            }

            // 加载上下文菜单
            foreach (var assembly in Application.Current.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IIEditorToolContextMenu).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (CreateContextBoundInstance(type, context) is IIEditorToolContextMenu instance)
                        {
                            IIEditorToolContextMenus.Add(instance);
                        }
                    }
                }
            }

            // 加载编辑器工具
            foreach (var assembly in Application.Current.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IEditorTool).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract && CanCreateGlobalEditorTool(type))
                    {
                        if (CreateEditorTool(type, context) is IEditorTool instance)
                        {
                            IEditorTools.Add(instance);
                            if (instance is IImageViewSettingProvider settingProvider)
                            {
                                imageView.RegisterImageViewSettingProvider(settingProvider);
                            }
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
                foreach (var type in assembly.GetTypes())
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

        public void RefreshToolBars()
        {
            foreach (FrameworkElement element in _generatedToolElements.ToArray())
            {
                if (element.Parent is ToolBar parentToolBar)
                {
                    parentToolBar.Items.Remove(element);
                }
            }
            _generatedToolElements.Clear();

            foreach (var group in GetEffectiveEditorTools().GroupBy(t => t.ToolBarLocal))
            {
                ToolBar? toolBar = _imageView.GetRegionToolBar(group.Key);
                if (toolBar == null)
                {
                    continue;
                }

                Thickness margin = GetSpacingFor(group.Key);
                bool hasExistingItems = toolBar.Items.Count > 0;
                int index = 0;
                foreach (IEditorTool tool in group.OrderBy(t => t.Order))
                {
                    FrameworkElement btn = GenIEditorTool(tool);
                    if (hasExistingItems || index++ > 0)
                    {
                        btn.Margin = margin;
                    }

                    toolBar.Items.Add(btn);
                    _generatedToolElements.Add(btn);
                    hasExistingItems = true;
                }
            }
        }

        public void Dispose()
        {
            if (_currentImageOpen is IImageOpenEditorToolLifecycle lifecycle)
            {
                lifecycle.OnEditorToolsDeactivated(_context);
            }

            HashSet<IDisposable> disposableTools = new();
            foreach (IDisposable item in IEditorTools.Concat(_imageOpenEditorTools).OfType<IDisposable>())
            {
                if (disposableTools.Add(item))
                {
                    item.Dispose();
                }
            }

            _imageOpenEditorTools.Clear();
            _generatedToolElements.Clear();
            GC.SuppressFinalize(this);
        }

        private static bool CanCreateGlobalEditorTool(Type type)
        {
            return SelectContextConstructor(type) != null;
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
        public static FrameworkElement GenIEditorTool(IEditorTool editorTool)
        {
            if (editorTool is IEditorCustomControlTool customControlTool)
            {
                return customControlTool.CreateToolControl();
            }
            else if (editorTool is IEditorToggleTool toggleTool)
            {
                var tbtn = new ToggleButton
                {
                    Height = 27,
                    Width = 27,
                    Padding = new Thickness(3),
                    Content = editorTool.Icon,
                    Command = editorTool.Command,
                    DataContext = toggleTool
                };

                // Bind IsChecked to the tool's IsChecked property (two-way)
                var binding = new Binding(nameof(IEditorToggleTool.IsChecked))
                {
                    Source = toggleTool,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                tbtn.SetBinding(ToggleButton.IsCheckedProperty, binding);
                return tbtn;
            }
            else if (editorTool is IEditorTextTool editorTextTool)
            {
                TextBox textBox = new TextBox()
                {
                    Background =Brushes.Transparent,
                    BorderThickness= new Thickness(1),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    DataContext = editorTextTool
                };
                textBox.SetBinding(TextBox.TextProperty, editorTextTool.Binding);
                return textBox;

            }
            else
            {
                var button = new Button() { Height = 27, Width = 27, Padding = new Thickness(3) };
                button.Content = editorTool.Icon;
                button.Command = editorTool.Command;
                return button;
            }
        }

        private static Thickness GetSpacingFor(ToolBarLocal loc)
        {
            return loc switch
            {
                ToolBarLocal.Top => new Thickness(5, 0, 0, 0),
                ToolBarLocal.Left => new Thickness(0, 5, 0, 0),
                ToolBarLocal.Right => new Thickness(0, 5, 0, 0),
                _ => new Thickness(5, 0, 0, 0)
            };
        }

        /// <summary>
        /// 尝试从资源中查找并返回图像
        /// </summary>
        public static Image TryFindResource(string resourcePath)
        {
            var image = new Image();
            // 动态资源引用，资源变更时自动更新
            image.SetResourceReference(Image.SourceProperty, resourcePath);
            return image;
        }
    }
}
