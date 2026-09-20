using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Tooling
{
    /// <summary>Owns toolbar attachments and generated controls, while tool instances remain owned by the extension host.</summary>
    internal sealed class EditorToolbarComposer
    {
        private readonly Func<ToolBarLocal, ToolBar?> _resolveToolbar;
        private readonly List<FrameworkElement> _generatedToolElements = new();
        private readonly HashSet<ButtonBase> _generatedIconHosts = new();

        internal EditorToolbarComposer(Func<ToolBarLocal, ToolBar?> resolveToolbar)
        {
            ArgumentNullException.ThrowIfNull(resolveToolbar);
            _resolveToolbar = resolveToolbar;
        }

        internal void Refresh(IEnumerable<IEditorTool> tools)
        {
            Clear();

            foreach (var group in tools.GroupBy(t => t.ToolBarLocal))
            {
                ToolBar? toolBar = _resolveToolbar(group.Key);
                if (toolBar == null)
                {
                    continue;
                }

                Thickness margin = GetSpacingFor(group.Key);
                bool hasExistingItems = toolBar.Items.Count > 0;
                int index = 0;
                foreach (IEditorTool tool in group.OrderBy(t => t.Order))
                {
                    FrameworkElement btn = CreateToolControl(tool);
                    if (hasExistingItems || index++ > 0)
                    {
                        btn.Margin = margin;
                    }

                    toolBar.Items.Add(btn);
                    _generatedToolElements.Add(btn);
                    if (tool is not IEditorCustomControlTool && btn is ButtonBase iconHost)
                    {
                        _generatedIconHosts.Add(iconHost);
                    }
                    hasExistingItems = true;
                }
            }
        }

        internal void Clear()
        {
            foreach (FrameworkElement element in _generatedToolElements)
            {
                if (element is ButtonBase buttonBase && _generatedIconHosts.Remove(buttonBase))
                {
                    buttonBase.Content = null;
                }

                if (element.Parent is ToolBar parentToolBar)
                {
                    parentToolBar.Items.Remove(element);
                }
            }

            _generatedToolElements.Clear();
            _generatedIconHosts.Clear();
        }

        internal static FrameworkElement CreateToolControl(IEditorTool editorTool)
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
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(1),
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

        internal static Image CreateResourceImage(string resourcePath)
        {
            var image = new Image();
            // 动态资源引用，资源变更时自动更新
            image.SetResourceReference(Image.SourceProperty, resourcePath);
            return image;
        }
    }
}
