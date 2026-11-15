using ColorVision.Algorithms;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.Properties;
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
            if (!_context.DrawCanvas.HasImage)
            {
                CreateStandardContextMenu(hasImage: false);
                e.Handled = _context.ContextMenu.Items.Count == 0;
                return;
            }

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
                    AddDrawingItems(selectVisual, selectEditorVisual);
                }
                else if (mouseVisual != null)
                {
                    AddDrawingItems(mouseVisual);
                }
            }

            if (_context.ContextMenu.Items.Count == 0)
            {
                CreateStandardContextMenu(hasImage: true);
            }

            e.Handled = _context.ContextMenu.Items.Count == 0;
        }

        internal void AddDrawingItems(object drawing, SelectEditorVisual? selection = null)
        {
            var analysis = new Dictionary<AlgorithmInteractiveGroupPresentation, List<MenuItem>>();
            AddFor(drawing);
            if (selection != null) AddFor(selection);
            if (analysis.Count == 0) return;

            MenuItem measurements = new() { Header = Resources.Algorithm_AlgorithmCalls };
            foreach (var (group, items) in analysis.OrderBy(entry => entry.Key.Order))
            {
                ItemCollection destination = measurements.Items;
                if (analysis.Count > 1)
                {
                    string? header = group.ResourceKey != null ? Resources.ResourceManager.GetString(group.ResourceKey, Resources.Culture) : null;
                    MenuItem category = new() { Header = header ?? group.DisplayName };
                    measurements.Items.Add(category);
                    destination = category.Items;
                }
                foreach (MenuItem item in items) destination.Add(item);
            }

            if (_context.ContextMenu.HasItems) _context.ContextMenu.Items.Add(new Separator());
            _context.ContextMenu.Items.Add(measurements);

            void AddFor(object target)
            {
                var providers = _context.IEditorToolFactory.ContextMenuProviders
                    .Where(provider => provider.ContextType.IsInstanceOfType(target))
                    .OrderBy(provider => provider is not DrawingVisualBaseDVContextMenu);
                foreach (var provider in providers)
                {
                    var group = provider.AnalysisGroup;
                    foreach (MenuItem item in provider.GetContextMenuItems(target))
                    {
                        if (item.Visibility != Visibility.Visible) continue;
                        if (group == null)
                        {
                            _context.ContextMenu.Items.Add(item);
                            continue;
                        }
                        if (!analysis.TryGetValue(group, out var items))
                        {
                            items = new List<MenuItem>();
                            analysis.Add(group, items);
                        }
                        items.Add(item);
                    }
                }
            }
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

        private void CreateStandardContextMenu(bool hasImage)
        {
            List<MenuItemMetadata> metadata = new();
            if (hasImage && (_context.ImageView.ImageContentCapability ?? (object?)_context.IImageOpen) is IIEditorToolContextMenu contentMenuProvider)
            {
                metadata.AddRange(contentMenuProvider.GetContextMenuItems());
            }

            foreach (var provider in _context.IEditorToolFactory.IIEditorToolContextMenus)
            {
                if (provider is not IImageOpen)
                {
                    metadata.AddRange(provider.GetContextMenuItems(hasImage));
                }
            }

            var groups = metadata.Where(item => item.Visibility == Visibility.Visible && item.OwnerGuid != null)
                .OrderBy(item => item.Order)
                .GroupBy(item => item.OwnerGuid!)
                .ToDictionary(group => group.Key);

            AddItems(_context.ContextMenu.Items, MenuItemConstants.Menu);

            void AddItems(ItemCollection destination, string ownerGuid)
            {
                if (!groups.Remove(ownerGuid, out var children)) return;

                int? previousOrder = null;
                foreach (MenuItemMetadata item in children)
                {
                    bool canExecute = item.Command is RoutedCommand routedCommand
                        ? routedCommand.CanExecute(null, _context.ImageView)
                        : item.Command?.CanExecute(null) ?? true;
                    if (!canExecute) continue;

                    MenuItem menuItem = new()
                    {
                        Header = item.Header,
                        Icon = item.Icon,
                        InputGestureText = item.InputGestureText,
                        CommandTarget = item.Command is RoutedCommand ? _context.ImageView : null,
                        Command = item.Command,
                        Tag = item,
                        IsChecked = item.IsChecked ?? false,
                    };

                    if (item.GuidId != null)
                    {
                        AddItems(menuItem.Items, item.GuidId);
                    }

                    if (item.Command == null && !menuItem.HasItems) continue;
                    if (previousOrder is int order && item.Order - order > 4)
                    {
                        destination.Add(new Separator());
                    }
                    destination.Add(menuItem);
                    previousOrder = item.Order;
                }
            }
        }

    }
}
