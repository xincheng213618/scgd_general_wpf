using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools;
using ColorVision.UI.Menus;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI
{
    public enum ToolBarLocal
    {
        Top,
        Bottom,
        Left,
        Right,
        RightBottom,
        LeftTop
    }

    public class EditorContext
    {
        public ImageViewModel ImageViewModel { get; set; }
        public DrawCanvas DrawCanvas { get; set; }
        public ZoomboxSub ZoomboxSub { get; set; }
    }

    public interface IEditorTool
    {
        public ToolBarLocal ToolBarLocal { get; }

        public string? GuidId { get; }
        public int Order { get; }

        public object Icon { get; }
        public ICommand? Command { get; }
    }

    public interface IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems();
    }


    public class IEditorToolFactory
    {
        public ObservableCollection<IEditorTool> IEditorTools { get; set; } = new ObservableCollection<IEditorTool>();
        public ObservableCollection<IIEditorToolContextMenu> IIEditorToolContextMenus { get; set; } = new ObservableCollection<IIEditorToolContextMenu>();

        public IEditorToolFactory(ImageView imageView,EditorContext context)
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

            var or = IEditorTools.OrderBy(a=>a.Order).ToList();

            for (int i = 0; i < or.Count; i++)
            {
                var editorTool = or[i];
                Button button = GenIEditorTool(editorTool);
                if (i != 0)
                {
                    button.Margin = new Thickness(5, 0, 0, 0);
                }
                imageView.ToolBarTop.Items.Add(button);
            }
        }

        public static Button GenIEditorTool(IEditorTool  editorTool)
        {
            Button button = new Button() { Height = 27, Width = 27, Padding = new Thickness(3) };
            button.Content = editorTool.Icon;
            button.Command = editorTool.Command;
            return button;

        }
    }

}
