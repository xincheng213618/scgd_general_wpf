using ColorVision.ImageEditor.Abstractions;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
    public class IEditorToolFactory
    {
        public T? GetIEditorTool<T>() where T : IEditorTool => IEditorTools.OfType<T>().FirstOrDefault();

        public ObservableCollection<IEditorTool> IEditorTools { get; set; } = new ObservableCollection<IEditorTool>();
        public ObservableCollection<IIEditorToolContextMenu> IIEditorToolContextMenus { get; set; } = new ObservableCollection<IIEditorToolContextMenu>();
        public ObservableCollection<IImageComponent> IImageComponents { get; set; } = new ObservableCollection<IImageComponent>();
        public Dictionary<string, IImageOpen> IImageOpens { get; set; } = new Dictionary<string, IImageOpen>();
        public ObservableCollection<IDVContextMenu> ContextMenuProviders { get; set; } = new ObservableCollection<IDVContextMenu>();
        
        /// <summary>
        /// Maps tool GuidId to its UI element for visibility control
        /// </summary>
        public Dictionary<string, FrameworkElement> ToolUIElements { get; set; } = new Dictionary<string, FrameworkElement>();


        public IEditorToolFactory(ImageView imageView, EditorContext context)
        {

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IDVContextMenu).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (Activator.CreateInstance(type) is IDVContextMenu instance)
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
                        if (Activator.CreateInstance(type, context) is IIEditorToolContextMenu instance)
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
                    if (typeof(IEditorTool).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (Activator.CreateInstance(type, context) is IEditorTool instance)
                        {
                            IEditorTools.Add(instance);
                        }
                    }
                }
            }

            // 将工具添加到对应的工具栏
            foreach (var group in IEditorTools.GroupBy(t => t.ToolBarLocal))
            {
                var toolBar = imageView.GetRegionToolBar(group.Key);
                if (toolBar == null) continue;

                var margin = GetSpacingFor(group.Key);
                int i = 0;
                foreach (var tool in group.OrderBy(t => t.Order))
                {
                    var btn = GenIEditorTool(tool);
                    if (i++ > 0) btn.Margin = margin; // 非第一个才加间距
                    toolBar.Items.Add(btn);
                    
                    // Store the UI element for this tool
                    if (!string.IsNullOrEmpty(tool.GuidId))
                    {
                        ToolUIElements[tool.GuidId] = btn;
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
        }

        /// <summary>
        /// 生成编辑器工具的 UI 元素
        /// </summary>
        /// <returns>Button 或 ToggleButton</returns>
        public static FrameworkElement GenIEditorTool(IEditorTool editorTool)
        {
            if (editorTool is IEditorToggleTool toggleTool)
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
