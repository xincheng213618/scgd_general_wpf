using ColorVision.UI;
using ColorVision.UI.Sorts;
using ScottPlot;
using ScottPlot.Plottables;
using Spectrum.Data;
using Spectrum.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Spectrum
{
    public partial class MainWindow
    {
        private ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listview && listview.SelectedIndex > -1)
            {
                DrawPlot();
                var selected = ViewResultSpectrums[listview.SelectedIndex];
                listView2.ItemsSource = selected.SpectralDatas;
                // Draw CIE points on both diagrams simultaneously
                DrawCIEPoinr(selected.fx, selected.fy, selected.fu, selected.fv);
            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ViewResultList.SelectedIndex > -1)
            {
                int temp = ViewResultList.SelectedIndex;
                ViewResultSpectrums.RemoveAt(ViewResultList.SelectedIndex);

                if (ViewResultList.Items.Count > 0)
                {
                    ViewResultList.SelectedIndex = temp - 1; ;
                    DrawPlot();
                }
                else
                {
                    wpfplot1.Plot.Clear();
                    AddSpectrumColorBar(wpfplot1);
                    wpfplot1.Refresh();
                }

            }
        }

        Marker markerPlot1;
        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            wpfplot1.Plot.Remove(markerPlot1);
            if (listView2.SelectedIndex > -1)
            {
                markerPlot1 = new Marker
                {
                    X = listView2.SelectedIndex + 380,
                    Y = ViewResultSpectrums[ViewResultList.SelectedIndex].fPL[listView2.SelectedIndex * 10],
                    MarkerShape = MarkerShape.FilledCircle,
                    MarkerSize = 10f,
                    Color = ScottPlot.Color.FromColor(System.Drawing.Color.Orange),
                };
                wpfplot1.Plot.PlottableList.Add(markerPlot1);
            }
            wpfplot1.Refresh();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ViewResultList.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ViewResultSpectrum);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    if (attribute != null)
                    {
                        string displayName = attribute.DisplayName;
                        displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                        if (displayName == gridViewColumnHeader.Content.ToString())
                        {
                            var item = GridViewColumnVisibilitys.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                            if (item != null)
                            {
                                item.IsSortD = !item.IsSortD;
                                ViewResultSpectrums.SortByProperty(property.Name, item.IsSortD);
                            }
                        }
                    }
                }
            }
        }

        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {

        }

        //清空数据
        private void Cleartable_Click(object sender, RoutedEventArgs e)
        {
            ViewResultSpectrums.Clear();
            ScatterPlots.Clear();
            AbsoluteScatterPlots.Clear();
            listView2.ItemsSource = new ObservableCollection<SpectralData>();
            if (ViewResultSpectrums.Count > 0)
            {
                ViewResultList.SelectedIndex = 0;
            }
            else
            {
                wpfplot1.Plot.Clear();
                AddSpectrumColorBar(wpfplot1);
                wpfplot1.Refresh();
                wpfplot2.Plot.Clear();
                wpfplot2.Refresh();
            }
            ReDrawPlot();
        }

        private void Delete()
        {
            if (ViewResultList.SelectedItems.Count == ViewResultList.Items.Count)
            {
                ViewResultManager.DeleteAllRecords();
            }
            else
            {
                var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
                ViewResultList.SelectedIndex = -1;
                ViewResultManager.DeleteSelected(selectedItems);
            }
        }

        /// <summary>
        /// Column-aware copy: extracts text from visible GridView columns for each selected item.
        /// Copies header + data rows (tab-separated) to clipboard.
        /// </summary>
        private void CopyVisibleColumns(object sender, ExecutedRoutedEventArgs e)
        {
            if (ViewResultList.View is not GridView gridView) return;
            var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
            if (selectedItems.Count == 0) return;

            // Collect visible columns and their binding paths
            var visibleColumns = new List<(string Header, string BindingPath)>();
            foreach (var col in gridView.Columns)
            {
                if (col.Width == 0) continue; // hidden column
                string header = col.Header?.ToString() ?? "";
                string path = "";

                if (col.DisplayMemberBinding is System.Windows.Data.Binding binding)
                {
                    path = binding.Path?.Path ?? "";
                }
                else if (col.CellTemplate is DataTemplate dt)
                {
                    // Extract binding path from the DataTemplate's TextBlock
                    var textBlock = dt.LoadContent() as System.Windows.Controls.TextBlock;
                    if (textBlock != null)
                    {
                        var tb = System.Windows.Data.BindingOperations.GetBinding(textBlock, System.Windows.Controls.TextBlock.TextProperty);
                        if (tb != null)
                            path = tb.Path?.Path ?? "";
                    }
                    // Also check for Border with Tag binding
                    if (string.IsNullOrEmpty(path))
                    {
                        var border = dt.LoadContent() as System.Windows.Controls.Border;
                        if (border != null)
                        {
                            var tagBinding = System.Windows.Data.BindingOperations.GetBinding(border, FrameworkElement.TagProperty);
                            if (tagBinding != null)
                                path = tagBinding.Path?.Path ?? "";
                        }
                    }
                }

                visibleColumns.Add((header, path));
            }

            var sb = new StringBuilder();
            // Header row
            sb.AppendLine(string.Join("\t", visibleColumns.Select(c => c.Header)));

            // Data rows
            var type = typeof(ViewResultSpectrum);
            foreach (var item in selectedItems)
            {
                var values = new List<string>();
                foreach (var (_, bindingPath) in visibleColumns)
                {
                    string val = "";
                    if (!string.IsNullOrEmpty(bindingPath))
                    {
                        var prop = type.GetProperty(bindingPath, BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null)
                        {
                            var v = prop.GetValue(item);
                            val = v?.ToString() ?? "";
                        }
                    }
                    values.Add(val);
                }
                sb.AppendLine(string.Join("\t", values));
            }

            try
            {
                Clipboard.SetText(sb.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                log.Warn("Failed to copy to clipboard", ex);
            }
        }

        private void GridViewColumnSort1(object sender, RoutedEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ViewResultList.Height = ListRow2.ActualHeight - 32;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.Instance.SaveConfigs();
        }

        public void Dispose()
        {
            Manager.SmuController.Close();
            logOutput?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
