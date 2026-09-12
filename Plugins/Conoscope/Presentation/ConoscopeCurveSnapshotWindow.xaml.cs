using ColorVision.Themes;
using Conoscope.Core;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Conoscope.Presentation
{
    public partial class ConoscopeCurveSnapshotWindow : Window
    {
        private readonly ObservableCollection<SnapshotRow> rows = new();
        private readonly ThemeManager themeManager = ThemeManager.Current;
        private DispatcherOperation? pendingThemeRefresh;
        private bool initialized;
        private bool closed;
        private int nextColorIndex;
        private bool isDirty;
        internal bool KeepSessionOnClose { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (KeepSessionOnClose)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnClosing(e);
        }

        public ConoscopeCurveSnapshotWindow()
        {
            InitializeComponent();
            SnapshotList.ItemsSource = rows;
            initialized = true;
            themeManager.CurrentUIThemeChanged += CurrentUIThemeChanged;
            Loaded += (_, _) => RefreshPlot(true);
            RefreshPlot(true);
        }

        public void AddSnapshot(ConoscopeCurveSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ObjectDisposedException.ThrowIf(closed, this);
            Dispatcher.VerifyAccess();
            SnapshotRow row = new(snapshot, nextColorIndex++);
            row.PropertyChanged += SnapshotRow_PropertyChanged;
            rows.Add(row);
            isDirty = true;
            SnapshotList.SelectedItem = row;
            SnapshotList.ScrollIntoView(row);
        }

        private void SnapshotList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshPlot(true);

        private void SnapshotRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SnapshotRow.Name) or nameof(SnapshotRow.IsShown)) isDirty = true;
            if (e.PropertyName == nameof(SnapshotRow.Name)) RefreshPlot(false);
            if (e.PropertyName == nameof(SnapshotRow.IsShown)) RefreshPlot(true);
        }

        private void SnapshotName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            SnapshotName.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }

        private void DeleteSnapshot_Click(object sender, RoutedEventArgs e)
        {
            if (SnapshotList.SelectedItem is not SnapshotRow selected) return;
            int index = rows.IndexOf(selected);
            selected.PropertyChanged -= SnapshotRow_PropertyChanged;
            rows.Remove(selected);
            isDirty = true;
            if (rows.Count > 0) SnapshotList.SelectedIndex = Math.Min(index, rows.Count - 1);
            RefreshPlot(true);
        }

        private void ExportSnapshot_Click(object sender, RoutedEventArgs e)
        {
            SnapshotName.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (SnapshotList.SelectedItem is not SnapshotRow selected) return;
            string fileName = string.Concat(selected.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim(' ', '.');
            SaveFileDialog dialog = new()
            {
                Filter = Properties.Resources.LabelSaveFilterCsv,
                FileName = $"{(fileName.Length == 0 ? "CurveSnapshot" : fileName)}.csv",
                DefaultExt = ".csv",
                AddExtension = true
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                ConoscopeAtomicFile.Write(dialog.FileName, selected.Snapshot.WriteCsv);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, ex.Message),
                    Properties.Resources.TitleCurveSnapshots, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal ConoscopeCurveSession CaptureSession()
        {
            SnapshotName.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            return new(rows.Select(row => new ConoscopeCurveSessionEntry(row.Snapshot, row.IsShown, row.ColorIndex)).ToArray(), SnapshotList.SelectedIndex);
        }

        internal void ImportSession(ConoscopeCurveSession session)
        {
            int offset = rows.Count;
            foreach (ConoscopeCurveSessionEntry entry in session.Entries)
            {
                SnapshotRow row = new(entry.Snapshot, entry.ColorIndex) { IsShown = entry.IsShown };
                row.PropertyChanged += SnapshotRow_PropertyChanged;
                rows.Add(row);
            }
            nextColorIndex = rows.Count;
            if (session.Entries.Count > 0)
            {
                SnapshotList.SelectedIndex = offset + Math.Max(0, session.SelectedIndex);
                isDirty = true;
            }
            RefreshPlot(true);
        }

        private void ImportSession_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new() { Filter = Properties.Resources.CurveSessionFilter };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                // Fully validate before appending; existing work is never replaced by import.
                ImportSession(ConoscopeCurveSessionFile.Load(dialog.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, CompositeFormatCache.Format(Properties.Resources.CurveSessionLoadFailed, ex.Message), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSession_Click(object sender, RoutedEventArgs e) => SaveSession(this);

        private bool SaveSession(Window owner)
        {
            SaveFileDialog dialog = new() { Filter = Properties.Resources.CurveSessionFilter, DefaultExt = ".conocurves", AddExtension = true, FileName = "Conoscope.conocurves" };
            if (dialog.ShowDialog(owner) != true) return false;
            try
            {
                ConoscopeCurveSessionFile.Save(dialog.FileName, CaptureSession());
                isDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, ex.Message), Title, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        internal bool ConfirmDiscardOrSave(Window owner)
        {
            SnapshotName.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (!isDirty) return true;
            MessageBoxResult result = MessageBox.Show(owner, Properties.Resources.CurveSessionUnsaved, Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return result == MessageBoxResult.No || (result == MessageBoxResult.Yes && SaveSession(owner));
        }

        private void RefreshPlot(bool autoScale)
        {
            if (!initialized || closed) return;
            SnapshotRow? selected = SnapshotList.SelectedItem as SnapshotRow;
            DeleteSnapshotButton.IsEnabled = selected != null;
            ExportSnapshotButton.IsEnabled = selected != null;
            SnapshotName.IsEnabled = selected != null;
            EmptyHint.Visibility = selected == null ? Visibility.Visible : Visibility.Collapsed;

            var limits = SnapshotPlot.Plot.Axes.GetLimits();
            SnapshotPlot.Plot.Clear();
            ConoscopePlotTheme.ApplyCartesian(SnapshotPlot, this);
            string font = ScottPlot.Fonts.Detect("中文");
            SnapshotPlot.Plot.Legend.FontName = font;
            SnapshotPlot.Plot.Axes.Left.Label.FontName = font;
            SnapshotPlot.Plot.Axes.Bottom.Label.FontName = font;
            SnapshotRow[] visible = selected == null ? Array.Empty<SnapshotRow>()
                : rows.Where(row => row.IsShown && row.Snapshot.IsCompatibleWith(selected.Snapshot)).ToArray();
            int incompatible = selected == null ? 0
                : rows.Count(row => row.IsShown && !row.Snapshot.IsCompatibleWith(selected.Snapshot));
            ComparisonSummary.Text = CompositeFormatCache.Format(Properties.Resources.SnapshotComparisonSummary, visible.Length, incompatible);

            foreach (SnapshotRow row in visible)
            {
                var scatter = SnapshotPlot.Plot.Add.Scatter(row.Snapshot.Positions.ToArray(), row.Snapshot.Values.ToArray());
                scatter.LegendText = $"{row.Name} · {row.Snapshot.SourceName} · {row.Snapshot.ChannelLabel}";
                scatter.LineWidth = 1.6f;
                scatter.MarkerSize = 0;
                Color color = row.ColorBrush.Color;
                scatter.Color = new ScottPlot.Color(color.R, color.G, color.B);
            }

            SnapshotPlot.Plot.XLabel(selected?.Snapshot.AxisLabel ?? string.Empty);
            SnapshotPlot.Plot.YLabel(selected == null ? string.Empty
                : string.IsNullOrEmpty(selected.Snapshot.UnitLabel) ? selected.Snapshot.ChannelLabel : selected.Snapshot.UnitLabel);
            SnapshotPlot.Plot.Legend.IsVisible = visible.Length > 0;
            if (autoScale && visible.Length > 0) SnapshotPlot.Plot.Axes.AutoScale();
            else if (!autoScale) SnapshotPlot.Plot.Axes.SetLimits(limits);
            SnapshotPlot.Refresh();
        }

        private void CurrentUIThemeChanged(Theme theme)
        {
            if (closed) return;
            pendingThemeRefresh?.Abort();
            pendingThemeRefresh = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                pendingThemeRefresh = null;
                if (closed) return;
                foreach (SnapshotRow row in rows) row.RefreshColor();
                RefreshPlot(false);
            }));
        }

        protected override void OnClosed(EventArgs e)
        {
            closed = true;
            pendingThemeRefresh?.Abort();
            themeManager.CurrentUIThemeChanged -= CurrentUIThemeChanged;
            foreach (SnapshotRow row in rows) row.PropertyChanged -= SnapshotRow_PropertyChanged;
            SnapshotPlot.Plot.Clear();
            rows.Clear();
            base.OnClosed(e);
        }

        private sealed class SnapshotRow : INotifyPropertyChanged
        {
            private static readonly string[] LightColors = { "#23856C", "#3D7CBB", "#A67116", "#9466B4", "#BD4A69", "#25869D" };
            private static readonly string[] DarkColors = { "#6DCAAA", "#80B7EE", "#E3B65D", "#C69DE4", "#E994A7", "#6AC6DD" };
            private readonly int colorIndex;
            public int ColorIndex => colorIndex;
            private bool isShown = true;
            public event PropertyChangedEventHandler? PropertyChanged;
            public ConoscopeCurveSnapshot Snapshot { get; private set; }

            public SnapshotRow(ConoscopeCurveSnapshot snapshot, int colorIndex)
            {
                Snapshot = snapshot;
                this.colorIndex = colorIndex;
            }

            public string Name
            {
                get => Snapshot.Name;
                set
                {
                    if (string.IsNullOrWhiteSpace(value) || string.Equals(Snapshot.Name, value.Trim(), StringComparison.Ordinal)) return;
                    Snapshot = Snapshot.WithName(value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }

            public bool IsShown
            {
                get => isShown;
                set
                {
                    if (isShown == value) return;
                    isShown = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsShown)));
                }
            }

            public string Description => $"{Snapshot.SourceName} · {Snapshot.ChannelLabel} · {Snapshot.ReferenceDescription}";

            public SolidColorBrush ColorBrush
            {
                get
                {
                    string[] palette = ThemeManager.Current.CurrentUITheme == Theme.Dark ? DarkColors : LightColors;
                    SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(palette[colorIndex % palette.Length]));
                    brush.Freeze();
                    return brush;
                }
            }

            public void RefreshColor() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorBrush)));

            public string Details
            {
                get
                {
                    StringBuilder text = new();
                    Add(Properties.Resources.SnapshotSource, Snapshot.SourceName);
                    if (!string.IsNullOrEmpty(Snapshot.SourcePath)) Add(Properties.Resources.CurveSourcePath, Snapshot.SourcePath);
                    Add(Properties.Resources.Con_Category_Model, Snapshot.ModelName);
                    Add(Properties.Resources.SnapshotCoordinates, Snapshot.CoordinateSystemName);
                    Add(Properties.Resources.SnapshotReference, Snapshot.ReferenceDescription);
                    Add(Properties.Resources.SnapshotChannel, Snapshot.ChannelLabel);
                    Add(Properties.Resources.SnapshotUnit, Snapshot.UnitLabel);
                    Add(Properties.Resources.SnapshotCapturedAt, Snapshot.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.CurrentCulture));
                    Add(Properties.Resources.SnapshotSamples, Snapshot.Positions.Count.ToString(CultureInfo.CurrentCulture));
                    if (!string.IsNullOrEmpty(Snapshot.Metadata)) Add(Properties.Resources.SnapshotMetadata, Snapshot.Metadata);
                    return text.ToString().TrimEnd();

                    void Add(string label, string value) => text.AppendLine($"{label}: {value}").AppendLine();
                }
            }
        }
    }
}
