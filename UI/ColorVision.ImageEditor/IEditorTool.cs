using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.ImageEditor
{
    public enum ToolBarLocal
    {
        Top,
        Draw,
        Left,
        Right,
        RightBottom,
        LeftTop
    }

    public interface IEditorTool
    {
        public ToolBarLocal ToolBarLocal { get; }

        public string? GuidId { get; }
        public int Order { get; }

        public object? Icon { get; }
        public ICommand? Command { get; }
    }

    // New: toggle-capable editor tool
    public interface IEditorToggleTool : IEditorTool
    {
        // Bindable toggle state. Use INotifyPropertyChanged on the implementation if the state can change programmatically.
        public bool IsChecked { get; set; }
    }

    public abstract class IEditorToggleToolBase : ViewModelBase, IEditorToggleTool
    {
        public virtual ToolBarLocal ToolBarLocal { get; set; } = ToolBarLocal.Draw;

        public virtual string? GuidId  => GetType().Name;

        public virtual int Order { get; set; } = 1;

        public virtual object Icon { get; set; }

        public virtual ICommand? Command { get; set; }

        public virtual bool IsChecked { get; set; }
    }


    public interface IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems();
    }
    
    public static class ToolBarLocalExtensions
    {

        public static ToolBar? GetRegionToolBar(this ImageView imageView, ToolBarLocal loc)
        {
            return loc switch
            {
                ToolBarLocal.Top => imageView.ToolBarTop.ToolBars.Count > 0 ? imageView.ToolBarTop.ToolBars[0] : null,
                ToolBarLocal.Left => imageView.ToolBarLeft.ToolBars.Count > 0 ? imageView.ToolBarLeft.ToolBars[0] : null,
                ToolBarLocal.Right => imageView.ToolBarRight.ToolBars.Count > 0 ? imageView.ToolBarRight.ToolBars[0] : null,
                ToolBarLocal.RightBottom => imageView.ToolBarRight.ToolBars.Count > 0 ? imageView.ToolBarRight.ToolBars[0] : null,
                ToolBarLocal.LeftTop => imageView.ToolBarLeft.ToolBars.Count > 0 ? imageView.ToolBarLeft.ToolBars[0] : null,
                ToolBarLocal.Draw => imageView.ToolBarDraw.ToolBars.Count > 0 ? imageView.ToolBarDraw.ToolBars[0] : null,
                _ => null
            };
        }
    }

    public class IEditorToolFactory
    {
        public ObservableCollection<IEditorTool> IEditorTools { get; set; } = new ObservableCollection<IEditorTool>();
        public ObservableCollection<IIEditorToolContextMenu> IIEditorToolContextMenus { get; set; } = new ObservableCollection<IIEditorToolContextMenu>();

        public ObservableCollection<IImageComponent> IImageComponents { get; set; } = new ObservableCollection<IImageComponent>();
        public Dictionary<string, IImageOpen> IImageOpens { get; set; } = new Dictionary<string, IImageOpen>();


        public IEditorToolFactory(ImageView imageView, EditorContext context)
        {
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
                }
            }


            foreach (var item in AssemblyService.Instance.LoadImplementations<IImageComponent>())
            {
                IImageComponents.Add(item);
            }

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

        // Updated: return FrameworkElement to allow both Button and ToggleButton
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
        
        public static Image TryFindResource(string resourcePath)
        {
            var image = new Image();
            // 动态资源引用，资源变更时自动更新
            image.SetResourceReference(Image.SourceProperty, resourcePath);
            return image;
        }
    }

}
