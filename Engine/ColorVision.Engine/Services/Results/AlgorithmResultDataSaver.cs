using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Results
{
    public static class AlgorithmResultDataSaver
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AlgorithmResultDataSaver));
        private static readonly object SaveMenuTag = new();

        public static RoutedUICommand SaveCommand { get; } = new(
            Properties.Resources.SaveDataColumn,
            nameof(SaveCommand),
            typeof(AlgorithmResultDataSaver));

        public static bool CanSave(ViewResultAlg? result)
        {
            if (result == null) return false;
            try
            {
                return FindHandler(result) != null;
            }
            catch (Exception)
            {
                // Report matching errors on an actual export, not on WPF command requery.
                return true;
            }
        }

        public static void Save(ViewResultContext context, ViewResultAlg result, string selectedPath)
        {
            var resultHandle = FindHandler(result);
            if (resultHandle == null)
                return;

            resultHandle.Load(context, result);
            resultHandle.SideSave(result, selectedPath);
        }

        internal static bool TrySave(ViewResultContext context, ViewResultAlg result, string selectedPath)
        {
            try
            {
                Save(context, result, selectedPath);
                return true;
            }
            catch (Exception error)
            {
                Log.Warn($"保存算法结果数据列失败：resultId={result.Id}, batchId={result.AlgResultMasterModel?.BatchId}, resultType={result.ResultType}, directory={selectedPath}", error);
                return false;
            }
        }

        internal static List<int> SaveSelected(ViewResultContext context, IEnumerable<ViewResultAlg> results, string selectedPath)
        {
            List<int> failures = [];
            foreach (ViewResultAlg result in results)
                if (!TrySave(context, result, selectedPath)) failures.Add(result.Id);
            return failures;
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

            List<int> failures = SaveSelected(context, selectedResults, dialog.SelectedPath);
            if (failures.Count == 0) return;

            const int maxShownIds = 10;
            string ids = string.Join(", ", failures.Take(maxShownIds));
            if (failures.Count > maxShownIds) ids += ", …";
            string message = string.Format(CultureInfo.CurrentCulture, EngineLocalization.Get("AlgorithmSideSaveFailures"), failures.Count, ids);
            MessageBox.Show(message, Properties.Resources.SaveDataColumn, MessageBoxButton.OK, MessageBoxImage.Warning);
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
