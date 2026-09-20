using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Tooling
{
    /// <summary>Composes context menus from the active drawing and extension contributions.</summary>
    internal sealed class ImageContextMenuComposer(EditorContext context)
    {
        private readonly EditorContext _context = context;

        internal void HandleOpening(object sender, ContextMenuEventArgs e)
        {
            _context.ContextMenu.Items.Clear();
            Point mouseDownPoint = Mouse.GetPosition(_context.DrawCanvas);

            if (TryCreateReferenceLineContextMenu())
            {
                return;
            }

            if (_context.IsImageEditMode)
            {
                Visual? mouseVisual = _context.DrawCanvas.GetVisual<Visual>(mouseDownPoint);
                if (mouseVisual is SelectEditorVisual selectEditorVisual && selectEditorVisual.GetVisual(mouseDownPoint) is ISelectVisual selectVisual)
                {
                    foreach (var provider in _context.IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(selectVisual);
                            foreach (var item in items)
                            {
                                _context.ContextMenu.Items.Add(item);
                            }
                        }
                    }

                    foreach (var provider in _context.IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectEditorVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(selectEditorVisual);
                            foreach (var item in items)
                            {
                                _context.ContextMenu.Items.Add(item);
                            }
                        }
                    }
                }
                else if (mouseVisual != null)
                {
                    foreach (var provider in _context.IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(mouseVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(mouseVisual);
                            foreach (var item in items)
                            {
                                _context.ContextMenu.Items.Add(item);
                            }
                        }
                    }
                }
            }

            if (_context.ContextMenu.Items.Count == 0)
            {
                CreateStandardContextMenu();
            }

            e.Handled = _context.ContextMenu.Items.Count == 0;
        }

        private bool TryCreateReferenceLineContextMenu()
        {
            ToolReferenceLine? referenceTool = _context.IEditorToolFactory.GetIEditorTool<ToolReferenceLine>();
            if (referenceTool?.IsChecked != true)
            {
                return false;
            }

            ReferenceLine referenceLine = referenceTool.ReferenceLine;
            foreach (var provider in _context.IEditorToolFactory.ContextMenuProviders)
            {
                if (!provider.ContextType.IsAssignableFrom(referenceLine.GetType()))
                {
                    continue;
                }

                foreach (var item in provider.GetContextMenuItems(referenceLine))
                {
                    _context.ContextMenu.Items.Add(item);
                }
            }

            return _context.ContextMenu.Items.Count > 0;
        }

        private void CreateStandardContextMenu()
        {
            List<MenuItemMetadata> menuItemMetadatas = new();
            if (_context.IImageOpen is IIEditorToolContextMenu contentMenuProvider)
            {
                menuItemMetadatas.AddRange(contentMenuProvider.GetContextMenuItems());
            }

            foreach (var item in _context.IEditorToolFactory.IIEditorToolContextMenus)
            {
                if (item is IImageOpen)
                {
                    continue;
                }

                menuItemMetadatas.AddRange(item.GetContextMenuItems());
            }

            List<MenuItemMetadata> sortedMenuItems = menuItemMetadatas.OrderBy(item => item.Order).ToList();

            void CreateMenu(MenuItem parentMenuItem, string ownerGuid)
            {
                List<MenuItemMetadata> childItems = sortedMenuItems.FindAll(item => item.OwnerGuid == ownerGuid).OrderBy(item => item.Order).ToList();
                for (int i = 0; i < childItems.Count; i++)
                {
                    MenuItemMetadata childItem = childItems[i];
                    string guidId = childItem.GuidId ?? Guid.NewGuid().ToString();
                    MenuItem menuItem = new()
                    {
                        Header = childItem.Header,
                        Icon = childItem.Icon,
                        InputGestureText = childItem.InputGestureText,
                        Command = childItem.Command,
                        Tag = childItem,
                        IsChecked = childItem.IsChecked ?? false,
                        Visibility = childItem.Visibility,
                    };

                    CreateMenu(menuItem, guidId);
                    if (i > 0 && childItem.Order - childItems[i - 1].Order > 4 && childItem.Visibility == Visibility.Visible)
                    {
                        parentMenuItem.Items.Add(new Separator());
                    }

                    parentMenuItem.Items.Add(menuItem);
                }

                foreach (MenuItemMetadata item in childItems)
                {
                    sortedMenuItems.Remove(item);
                }
            }

            List<MenuItemMetadata> rootItems = menuItemMetadatas
                .Where(item => item.OwnerGuid == MenuItemConstants.Menu && item.Visibility == Visibility.Visible)
                .OrderBy(item => item.Order)
                .ToList();

            for (int i = 0; i < rootItems.Count; i++)
            {
                MenuItemMetadata menuItemMeta = rootItems[i];
                MenuItem menuItem = new()
                {
                    Header = menuItemMeta.Header,
                    Command = menuItemMeta.Command,
                    Icon = menuItemMeta.Icon,
                    InputGestureText = menuItemMeta.InputGestureText,
                    IsChecked = menuItemMeta.IsChecked ?? false,
                };

                if (menuItemMeta.GuidId != null)
                {
                    CreateMenu(menuItem, menuItemMeta.GuidId);
                }

                if (i > 0 && menuItemMeta.Order - rootItems[i - 1].Order > 4)
                {
                    _context.ContextMenu.Items.Add(new Separator());
                }

                _context.ContextMenu.Items.Add(menuItem);
            }
        }

    }
}
