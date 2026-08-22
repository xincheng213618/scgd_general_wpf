using ColorVision.Engine.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Results
{
    public static class AlgorithmResultDataSaver
    {
        private static readonly object SaveMenuTag = new();

        public static RoutedUICommand SaveCommand { get; } = new(
            Properties.Resources.SaveDataColumn,
            nameof(SaveCommand),
            typeof(AlgorithmResultDataSaver));

        public static bool CanSave(ViewResultAlg? result)
        {
            return result != null && FindHandler(result) != null;
        }

        public static void Save(ViewResultContext context, ViewResultAlg result, string selectedPath)
        {
            var resultHandle = FindHandler(result);
            if (resultHandle == null)
                return;

            resultHandle.Load(context, result);
            resultHandle.SideSave(result, selectedPath);
        }

        public static void PromptAndSave(ViewResultContext context, IEnumerable<ViewResultAlg> results)
        {
            List<ViewResultAlg> selectedResults = results.Where(CanSave).ToList();
            if (selectedResults.Count == 0)
            {
                MessageBox.Show(Properties.Resources.SelectDataFirst);
                return;
            }

            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Properties.Resources.SelectSaveFolder,
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            foreach (ViewResultAlg result in selectedResults)
                Save(context, result, dialog.SelectedPath);
        }

        public static void EnsureContextMenu(ViewResultAlg result)
        {
            if (result.ContextMenu.Items.OfType<MenuItem>().Any(item => ReferenceEquals(item.Tag, SaveMenuTag)))
                return;

            result.ContextMenu.Items.Add(new Separator());
            MenuItem saveDataMenuItem = new()
            {
                Header = Properties.Resources.SaveDataColumn,
                Command = SaveCommand,
                CommandParameter = result,
                Tag = SaveMenuTag
            };
            BindingOperations.SetBinding(saveDataMenuItem, MenuItem.CommandTargetProperty, new Binding(nameof(ContextMenu.PlacementTarget))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContextMenu), 1)
            });
            result.ContextMenu.Items.Add(saveDataMenuItem);
        }

        private static IResultHandleBase? FindHandler(ViewResultAlg result)
        {
            return ResultHandleRegistry.GetInstance().ResultHandles.FirstOrDefault(item => item.CanHandle1(result));
        }
    }
}
