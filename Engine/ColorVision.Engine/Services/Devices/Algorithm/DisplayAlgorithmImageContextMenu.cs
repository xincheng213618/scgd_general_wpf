using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public sealed class DisplayAlgorithmImageContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        private const string RootGuid = "DisplayAlgorithm.Recalculate";
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            if (!TryGetImageFilePath(out string imageFilePath))
            {
                return new List<MenuItemMetadata>();
            }

            DisplayAlgorithmVisibilityConfig visibilityConfig = DisplayAlgorithmVisibilityConfig.Instance;
            List<DisplayAlgorithmMeta> algorithms = DisplayAlgorithmManager.GetInstance().AlgorithmMetas
                .Where(meta => visibilityConfig.GetAlgorithmVisibility(meta.Name))
                .OrderBy(meta => meta.Group)
                .ThenBy(meta => visibilityConfig.GetOrderOverride(meta.Name, meta.Order))
                .ToList();

            if (algorithms.Count == 0)
            {
                return new List<MenuItemMetadata>();
            }

            List<MenuItemMetadata> menuItems =
            [
                new MenuItemMetadata
                {
                    GuidId = RootGuid,
                    Order = 105,
                    Header = Properties.Resources.DisplayAlgorithmRecalculate,
                    Visibility = Visibility.Visible
                }
            ];

            int groupOrder = 0;
            foreach (IGrouping<string, DisplayAlgorithmMeta> group in algorithms.GroupBy(meta => meta.Group))
            {
                string groupName = string.IsNullOrWhiteSpace(group.Key) ? "Other" : group.Key;
                string groupGuid = $"{RootGuid}.Group.{groupOrder}";
                menuItems.Add(new MenuItemMetadata
                {
                    OwnerGuid = RootGuid,
                    GuidId = groupGuid,
                    Order = groupOrder++,
                    Header = groupName,
                    Visibility = Visibility.Visible
                });

                foreach (DisplayAlgorithmMeta meta in group)
                {
                    RelayCommand command = new(
                        _ => OpenAlgorithm(meta, imageFilePath),
                        _ => IsImageFilePathCurrent(imageFilePath));

                    menuItems.Add(new MenuItemMetadata
                    {
                        OwnerGuid = groupGuid,
                        GuidId = $"{RootGuid}.Algorithm.{meta.Type.FullName}",
                        Order = visibilityConfig.GetOrderOverride(meta.Name, meta.Order),
                        Header = visibilityConfig.GetNameOverride(meta.Name, meta.DisplayName),
                        Command = command,
                        Visibility = Visibility.Visible
                    });
                }
            }

            return menuItems;
        }

        private void OpenAlgorithm(DisplayAlgorithmMeta meta, string imageFilePath)
        {
            if (!IsImageFilePathCurrent(imageFilePath))
            {
                return;
            }

            if (!File.Exists(imageFilePath))
            {
                ColorVision.Themes.Controls.MessageBox1.Show(
                    context.OwnerWindow,
                    string.Format(Properties.Resources.LocalImage_FileNotFound, imageFilePath),
                    "ColorVision");
                return;
            }

            DisplayAlgorithmManager.GetInstance().OpenWindow(
                new DisplayAlgorithmParam
                {
                    Type = meta.Type,
                    ImageFilePath = imageFilePath
                },
                context.OwnerWindow);
        }

        private bool TryGetImageFilePath(out string imageFilePath)
        {
            imageFilePath = context.Config.FilePath;
            return IsImageFilePathCurrent(imageFilePath);
        }

        private bool IsImageFilePathCurrent(string imageFilePath)
        {
            return context.IImageOpen != null &&
                   context.ImageView.ViewBitmapSource != null &&
                   !string.IsNullOrWhiteSpace(imageFilePath) &&
                   string.Equals(context.Config.FilePath, imageFilePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
