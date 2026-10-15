using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using ColorVision.UI;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Editing;

public partial class CalibrationJsonEditorWindow : Window
{
    private readonly ServiceTypes type;
    private CalibrationJsonFileSnapshot? snapshot;
    private CalibrationJsonDocument document = null!;
    private bool initializing, advancedPending, saving, changed, closeAfterSave;
    public CalibrationJsonDocument Document => document;
    public bool HasUnsavedChanges => changed || advancedPending;
    public string? SavedFilePath { get; private set; }

    public CalibrationJsonEditorWindow(ServiceTypes type, string? filePath = null)
    {
        this.type = type;
        initializing = true;
        InitializeComponent();
        this.ApplyCaption();
        CurveTab.Visibility = type is ServiceTypes.ColorDiff or ServiceTypes.AngleShift ? Visibility.Visible : Visibility.Collapsed;
        SpatialTab.Visibility = type is ServiceTypes.ColorShift or ServiceTypes.ColorDiff or ServiceTypes.AngleShift or ServiceTypes.Distortion ? Visibility.Visible : Visibility.Collapsed;
        PreviewSizePanel.Visibility = type == ServiceTypes.ColorShift ? Visibility.Visible : Visibility.Collapsed;
        CurveDescription.Text = type == ServiceTypes.AngleShift
            ? "横轴为放大图中距光学中心的半径（原图半径 × 放大倍率），纵轴为径向位移。"
            : "横轴为距光学中心的像素半径，纵轴为距离比例修正后的径向位移。";
        if (filePath == null)
        {
            document = CalibrationJsonDocument.CreateDefault(type);
            ManualConfirmation.Visibility = Visibility.Visible;
            changed = true;
        }
        else
        {
            snapshot = CalibrationJsonFileStore.Load(filePath);
            document = CalibrationJsonDocument.Parse(type, snapshot.Json);
        }
        SetDocument(document);
        initializing = false;
        UpdateStatus();
    }

    private void SetDocument(CalibrationJsonDocument value)
    {
        if (document != null)
            foreach (var parameter in AllParameters(document)) parameter.PropertyChanged -= ParameterChanged;
        document = value;
        foreach (var parameter in AllParameters(document)) parameter.PropertyChanged += ParameterChanged;
        HeadingText.Text = document.Title + " · 校正参数";
        Title = HeadingText.Text;
        DescriptionText.Text = document.Description;
        PathText.Text = snapshot?.Path ?? "新建手工参数文件：请填写实际参数并核对后保存。";
        BuildParameterPanel();
        initializing = true;
        AdvancedJson.Text = document.Json.ToString(Newtonsoft.Json.Formatting.Indented);
        advancedPending = false;
        initializing = false;
        DrawCurves();
        DrawSpatial();
    }

    private static IEnumerable<CalibrationScalarParameter> AllParameters(CalibrationJsonDocument value)
        => value.Parameters.Concat(value.Tables.SelectMany(table => table.Rows));

    private Border Card(string title, UIElement content)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        panel.Children.Add(content);
        return new Border { Child = panel, Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 12), Style = (Style)FindResource("UpdateDialogCardStyle") };
    }

    private void BuildParameterPanel()
    {
        ParameterPanel.Children.Clear();
        bool matrixFirst = type is ServiceTypes.LumFourColor or ServiceTypes.LumMultiColor;
        void AddTable(CalibrationCoefficientTable table)
        {
            UIElement editor = (table.Key is "matrix" or "pa" or "cameraMatrix") && table.Rows.Count >= 9
                ? BuildMatrix(table) : BuildCoefficientTable(table);
            ParameterPanel.Children.Add(Card(table.Title, editor));
        }
        if (matrixFirst)
            foreach (var table in document.Tables.Where(table => table.Key is "matrix" or "pa")) AddTable(table);
        foreach (var group in document.Parameters.GroupBy(parameter => parameter.Group))
        {
            var fields = new StackPanel();
            foreach (var parameter in group)
            {
                var adapter = new ParameterAdapter(parameter);
                string name = parameter.Kind == CalibrationParameterKind.Boolean ? nameof(ParameterAdapter.BooleanValue) : nameof(ParameterAdapter.TextValue);
                PropertyInfo property = typeof(ParameterAdapter).GetProperty(name)!;
                DockPanel row = PropertyEditorHelper.GenProperties(property, adapter);
                row.Margin = new Thickness(0, 3, 0, 3);
                row.ToolTip = parameter.Description;
                if (row.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock label)
                {
                    label.Text = parameter.Label;
                    label.Width = 180;
                }
                AutomationProperties.SetName(row, parameter.Label + " " + parameter.Path);
                fields.Children.Add(row);
            }
            bool compatibility = group.Key == "采集参数" && type is ServiceTypes.LumFourColor or ServiceTypes.Luminance;
            ParameterPanel.Children.Add(compatibility
                ? Card("历史采集信息", new Expander { Header = "展开查看原文件信息（不参与当前转换）", Content = fields })
                : Card(group.Key, fields));
        }
        foreach (var table in document.Tables)
        {
            if (matrixFirst && table.Key is "matrix" or "pa") continue;
            AddTable(table);
        }
    }

    private UIElement BuildCoefficientTable(CalibrationCoefficientTable table)
    {
        var grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, ItemsSource = table.Rows, MinHeight = 90, MaxHeight = 320, HeadersVisibility = DataGridHeadersVisibility.Column, EnableRowVirtualization = true };
        grid.Columns.Add(new DataGridTextColumn { Header = "系数 / 位置", Binding = new Binding(nameof(CalibrationScalarParameter.Label)), IsReadOnly = true, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "数值", Binding = new Binding(nameof(CalibrationScalarParameter.Value)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        if (type is not (ServiceTypes.ColorDiff or ServiceTypes.AngleShift) || !(table.Key.StartsWith("ColorDiffCoeffs_", StringComparison.Ordinal) || table.Key.StartsWith("coeff_", StringComparison.Ordinal))) return grid;
        var panel = new StackPanel();
        panel.Children.Add(grid);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var add = new Button { Content = "增加高阶项", Style = (Style)FindResource("UpdateDialogSecondaryButtonStyle") };
        var remove = new Button { Content = "删除末项", Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource("UpdateDialogSecondaryButtonStyle"), IsEnabled = table.Rows.Count > 1 };
        add.Click += (_, _) => ResizePolynomial(table.Key, 1);
        remove.Click += (_, _) => ResizePolynomial(table.Key, -1);
        actions.Children.Add(add); actions.Children.Add(remove); panel.Children.Add(actions);
        if (type == ServiceTypes.AngleShift) panel.Children.Add(new TextBlock { Text = "RGB 三组系数和多项式阶数会同步调整。", Margin = new Thickness(0, 6, 0, 0) });
        return panel;
    }

    private void ResizePolynomial(string key, int delta)
    {
        try
        {
            JObject json = JObject.Parse(document.BuildDraftJson());
            string[] keys = type == ServiceTypes.AngleShift ? ["coeff_r", "coeff_g", "coeff_b"] : [key];
            if (keys.Any(item => json[item] is not JArray)) throw new FormatException("请先在高级 JSON 中补全系数数组。");
            if (type == ServiceTypes.AngleShift && keys.Select(item => ((JArray)json[item]!).Count).Distinct().Count() != 1) throw new FormatException("RGB 系数数组长度不一致，请先核对。");
            foreach (string item in keys)
            {
                var array = (JArray)json[item]!;
                if (delta > 0) array.Add(0.0);
                else if (array.Count > 1) array.RemoveAt(array.Count - 1);
            }
            if (type == ServiceTypes.AngleShift) json["coefficient_order"] = ((JArray)json["coeff_r"]!).Count - 1;
            SetDocument(CalibrationJsonDocument.Parse(type, json.ToString()));
            changed = true;
            UpdateStatus();
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private static UIElement BuildMatrix(CalibrationCoefficientTable table)
    {
        var panel = new StackPanel();
        var matrix = new Grid();
        matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });
        for (int column = 0; column < 3; column++) matrix.ColumnDefinitions.Add(new ColumnDefinition());
        for (int row = 0; row < 4; row++) matrix.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        string[] columns = table.Key == "cameraMatrix" ? ["第 1 列", "第 2 列", "第 3 列"] : ["R", "G", "B"];
        string[] rows = table.Key == "cameraMatrix" ? ["第 1 行", "第 2 行", "第 3 行"] : ["X", "Y", "Z"];
        for (int column = 0; column < 3; column++)
        {
            var heading = new TextBlock { Text = columns[column], HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(8) };
            Grid.SetColumn(heading, column + 1); matrix.Children.Add(heading);
        }
        for (int row = 0; row < 3; row++)
        {
            var heading = new TextBlock { Text = rows[row], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
            Grid.SetRow(heading, row + 1); matrix.Children.Add(heading);
            for (int column = 0; column < 3; column++)
            {
                var parameter = table.Rows[row * 3 + column];
                var input = new TextBox { Margin = new Thickness(5), MinWidth = 100, ToolTip = parameter.Path, HorizontalContentAlignment = HorizontalAlignment.Right };
                input.SetBinding(TextBox.TextProperty, new Binding(nameof(CalibrationScalarParameter.Value)) { Source = parameter, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                AutomationProperties.SetName(input, table.Title + " " + rows[row] + "/" + columns[column]);
                Grid.SetRow(input, row + 1); Grid.SetColumn(input, column + 1); matrix.Children.Add(input);
            }
        }
        panel.Children.Add(matrix);
        if (table.Rows.Count > 9) panel.Children.Add(new TextBlock { Text = "矩阵后的扩展数据会保留，可在高级 JSON 中查看。", Margin = new Thickness(8, 10, 8, 0) });
        return panel;
    }

    private void ParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (initializing) return;
        changed = true;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (StatusText == null || document == null) return;
        var errors = document.Validate();
        StatusText.Text = errors.Count == 0 ? (HasUnsavedChanges ? "参数已修改，尚未保存。" : "参数检查通过。") : string.Join("\n", errors.Take(5));
        SaveButton.IsEnabled = !saving && (!IsManualDraft || ManualConfirmation.IsChecked == true);
        SaveAsButton.IsEnabled = SaveButton.IsEnabled;
        CommandManager.InvalidateRequerySuggested();
    }
    private bool IsManualDraft => ManualConfirmation.Visibility == Visibility.Visible;

    private bool ApplyAdvanced()
    {
        if (!advancedPending) return true;
        try
        {
            var parsed = CalibrationJsonDocument.Parse(type, AdvancedJson.Text);
            var errors = parsed.Validate();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors.Take(8)));
            SetDocument(parsed);
            changed = true;
            UpdateStatus();
            return true;
        }
        catch (Exception ex) { StatusText.Text = ex.Message; return false; }
    }

    private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (initializing || !ReferenceEquals(e.Source, EditorTabs) || document == null) return;
        if (ReferenceEquals(EditorTabs.SelectedItem, AdvancedTab) && !advancedPending)
        {
            try { initializing = true; AdvancedJson.Text = document.BuildDraftJson(); }
            catch (Exception ex) { StatusText.Text = ex.Message; EditorTabs.SelectedItem = ParameterTab; }
            finally { initializing = false; }
        }
        else if (!ReferenceEquals(EditorTabs.SelectedItem, AdvancedTab) && advancedPending && !ApplyAdvanced())
        {
            initializing = true; EditorTabs.SelectedItem = AdvancedTab; initializing = false;
        }
        if (ReferenceEquals(EditorTabs.SelectedItem, CurveTab)) DrawCurves();
        if (ReferenceEquals(EditorTabs.SelectedItem, SpatialTab)) DrawSpatial();
    }

    private void AdvancedJson_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (initializing || document == null) return;
        advancedPending = true;
        changed = true;
        StatusText.Text = "JSON 已修改，应用或保存时会检查。";
    }
    private void ApplyAdvanced_Click(object sender, RoutedEventArgs e) => ApplyAdvanced();
    private void Confirmation_Changed(object sender, RoutedEventArgs e) => UpdateStatus();
    private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = document != null && !saving && (!IsManualDraft || ManualConfirmation.IsChecked == true);
    private async void Save_Executed(object sender, ExecutedRoutedEventArgs e) => await SaveAsync(false);
    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync(false);
    private async void SaveAs_Click(object sender, RoutedEventArgs e) => await SaveAsync(true);

    private async Task<bool> SaveAsync(bool saveAs)
    {
        if (saving || (IsManualDraft && ManualConfirmation.IsChecked != true) || !ApplyAdvanced()) return false;
        try
        {
            string json = document.BuildJson();
            string? destination = null;
            if (saveAs || snapshot == null)
            {
                var dialog = new SaveFileDialog { Title = "保存校正参数", Filter = "校正文件|*.dat;*.json|所有文件|*.*", FileName = snapshot == null ? document.Type + ".dat" : System.IO.Path.GetFileNameWithoutExtension(snapshot.Path) + "_edited.dat", OverwritePrompt = false };
                if (dialog.ShowDialog(this) != true) return false;
                destination = dialog.FileName;
            }
            saving = true;
            EditorTabs.IsEnabled = false;
            UpdateStatus();
            CalibrationJsonFileSnapshot result = await Task.Run(() => destination == null
                ? CalibrationJsonFileStore.Save(snapshot!, json)
                : CalibrationJsonFileStore.SaveAs(destination, json, snapshot));
            snapshot = result;
            SavedFilePath = result.Path;
            ManualConfirmation.Visibility = Visibility.Collapsed;
            SetDocument(CalibrationJsonDocument.Parse(type, result.Json));
            changed = false;
            StatusText.Text = result.LastBackupPath == null ? "已保存：" + result.Path : "已保存，原文件备份：" + result.LastBackupPath;
            if (closeAfterSave) { saving = false; Close(); }
            return true;
        }
        catch (Exception ex) { StatusText.Text = ex.Message; return false; }
        finally { saving = false; EditorTabs.IsEnabled = true; UpdateButtons(); }
    }

    private void UpdateButtons()
    {
        SaveButton.IsEnabled = !saving && (!IsManualDraft || ManualConfirmation.IsChecked == true);
        SaveAsButton.IsEnabled = SaveButton.IsEnabled;
        CommandManager.InvalidateRequerySuggested();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (saving || snapshot == null) return;
        if (HasUnsavedChanges && MessageBox.Show(this, "重新加载会放弃尚未保存的修改，是否继续？", Title, MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        try
        {
            snapshot = CalibrationJsonFileStore.Load(snapshot.Path);
            SetDocument(CalibrationJsonDocument.Parse(type, snapshot.Json));
            changed = false;
            UpdateStatus();
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (saving) { e.Cancel = true; return; }
        if (!HasUnsavedChanges) return;
        var answer = MessageBox.Show(this, "校正参数尚未保存。是否保存后关闭？", Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.No) return;
        e.Cancel = true;
        if (answer == MessageBoxResult.Yes) { closeAfterSave = true; if (!await SaveAsync(false)) closeAfterSave = false; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void RefreshCurve_Click(object sender, RoutedEventArgs e) => DrawCurves();
    private void CurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) { if (!initializing) DrawCurves(); }
    private void CurveRadius_TextChanged(object sender, TextChangedEventArgs e) { if (!initializing) DrawCurves(); }

    private void RefreshSpatial_Click(object sender, RoutedEventArgs e) => DrawSpatial();
    private void SpatialCanvas_SizeChanged(object sender, SizeChangedEventArgs e) { if (!initializing) DrawSpatial(); }

    private void DrawSpatial()
    {
        if (SpatialCanvas == null || document == null || SpatialCanvas.ActualWidth <= 1 || SpatialCanvas.ActualHeight <= 1 || SpatialTab.Visibility != Visibility.Visible) return;
        SpatialCanvas.Children.Clear();
        try
        {
            if (!int.TryParse(PreviewWidth.Text, out int width) || !int.TryParse(PreviewHeight.Text, out int height) || width <= 0 || height <= 0) throw new FormatException("预览宽高必须是正整数。");
            var preview = CalibrationSpatialPreview.Create(document, width, height);
            const double margin = 20;
            double scale = Math.Min(Math.Max(1, SpatialCanvas.ActualWidth - margin * 2) / preview.Width, Math.Max(1, SpatialCanvas.ActualHeight - margin * 2) / preview.Height);
            double left = (SpatialCanvas.ActualWidth - preview.Width * scale) / 2, top = (SpatialCanvas.ActualHeight - preview.Height * scale) / 2;
            Brush[] colors = [Brushes.IndianRed, Brushes.SeaGreen, Brushes.RoyalBlue];
            void Draw(IReadOnlyList<IReadOnlyList<Point>> lines, Brush brush, double opacity)
            {
                foreach (var points in lines)
                {
                    var line = new System.Windows.Shapes.Polyline { Stroke = brush, StrokeThickness = 1.3, Opacity = opacity };
                    foreach (var point in points) line.Points.Add(new Point(left + point.X * scale, top + point.Y * scale));
                    SpatialCanvas.Children.Add(line);
                }
            }
            Draw(preview.ReferenceGrid, Brushes.Gray, 0.4);
            foreach (var series in preview.Series) Draw(series.Lines, colors[series.ColorIndex], 0.8);
            SpatialStatus.Text = string.Join(" / ", preview.Series.Select(series => series.Name)) + "；" + preview.Note;
        }
        catch (Exception ex) { SpatialStatus.Text = ex.Message; }
    }

    private void DrawCurves()
    {
        if (CurveCanvas == null || document == null || CurveCanvas.ActualWidth <= 1 || CurveCanvas.ActualHeight <= 1) return;
        CurveCanvas.Children.Clear();
        if (type is not (ServiceTypes.ColorDiff or ServiceTypes.AngleShift))
        { CurveStatus.Text = "此校正使用偏移或矩阵参数，不包含径向多项式。"; return; }
        if (!double.TryParse(CurveRadius.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double radius) || !double.IsFinite(radius) || radius <= 0)
        { CurveStatus.Text = "最大半径必须是大于零的有限数值。"; return; }
        try
        {
            JObject json = JObject.Parse(document.BuildJson());
            string[] keys = type == ServiceTypes.ColorDiff ? ["ColorDiffCoeffs_GR", "ColorDiffCoeffs_GB"] : ["coeff_r", "coeff_g", "coeff_b"];
            var series = keys.Select(key => (Key: key, Coefficients: ((JArray)json[key]!).Values<double>().ToArray())).ToArray();
            var values = series.Select(seriesItem => Enumerable.Range(0, 129).Select(index =>
            {
                double r = radius * index / 128, value = 0;
                for (int coefficient = seriesItem.Coefficients.Length - 1; coefficient >= 0; coefficient--) value = value * r + seriesItem.Coefficients[coefficient];
                if (type == ServiceTypes.ColorDiff) value *= json["CalibDis"]!.Value<double>() / json["MeasDis"]!.Value<double>();
                if (!double.IsFinite(value)) throw new InvalidOperationException("当前系数在所选半径内溢出，请减小半径或核对参数。");
                return value;
            }).ToArray()).ToArray();
            double minimum = Math.Min(0, values.Min(items => items.Min())), maximum = Math.Max(0, values.Max(items => items.Max()));
            if (maximum == minimum) maximum = minimum + 1;
            if (!double.IsFinite(maximum - minimum)) throw new InvalidOperationException("位移跨度超出预览范围，请核对系数和半径。");
            const double margin = 45;
            double width = Math.Max(1, CurveCanvas.ActualWidth - margin * 2), height = Math.Max(1, CurveCanvas.ActualHeight - margin * 2);
            Brush[] colors = type == ServiceTypes.ColorDiff ? [Brushes.OrangeRed, Brushes.RoyalBlue] : [Brushes.IndianRed, Brushes.SeaGreen, Brushes.RoyalBlue];
            for (int line = 0; line < values.Length; line++)
            {
                var polyline = new System.Windows.Shapes.Polyline { Stroke = colors[line], StrokeThickness = 2 };
                for (int index = 0; index < values[line].Length; index++) polyline.Points.Add(new Point(margin + width * index / 128, margin + height * (1 - (values[line][index] - minimum) / (maximum - minimum))));
                CurveCanvas.Children.Add(polyline);
            }
            var axis = new TextBlock { Text = $"0 → {radius:G5} px       位移范围 {minimum:G5} → {maximum:G5} px", Foreground = (Brush)FindResource("UpdateDialog.TextSecondary") };
            Canvas.SetLeft(axis, margin); Canvas.SetBottom(axis, 8); CurveCanvas.Children.Add(axis);
            CurveStatus.Text = string.Join(" / ", keys) + "；系数按常数项、一次项、二次项排列。曲线展示参数函数，不代表实测标定质量。";
        }
        catch (Exception ex) { CurveStatus.Text = ex.Message; }
    }

    private sealed class ParameterAdapter(CalibrationScalarParameter parameter) : ViewModelBase
    {
        [Category("校正参数"), DisplayName("参数"), Description("校正文件数值，保存时检查格式与适用约束。")]
        public string TextValue { get => parameter.Value; set { parameter.Value = value; OnPropertyChanged(); } }
        [Category("校正参数"), DisplayName("参数"), Description("校正文件布尔选项。")]
        public bool BooleanValue { get => bool.TryParse(parameter.Value, out bool value) && value; set { parameter.Value = value ? "true" : "false"; OnPropertyChanged(); } }
    }
}
