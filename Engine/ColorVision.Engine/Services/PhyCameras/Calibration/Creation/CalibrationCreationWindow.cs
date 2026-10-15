using cvColorVision;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Themes;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Creation;

public sealed class CalibrationCreationWindow : Window
{
    private readonly ComboBox type = new() { Margin = new Thickness(0, 6, 0, 10) };
    private readonly TextBox radius = new() { Text = "50", Margin = new Thickness(0, 4, 0, 10) };
    private readonly TextBox threshold = new() { Text = "100", Margin = new Thickness(0, 4, 0, 10) };
    private readonly CheckBox bright = new() { Content = "检测亮缺陷（暗场）；取消则检测暗缺陷（亮场）", IsChecked = true };
    private readonly CheckBox rawConfirmed = new() { Content = "我确认采集这些图像时已关闭全部校正", Margin = new Thickness(0, 8, 0, 8) };
    private readonly CheckBox interleaved = new() { Content = "三通道文件为交错 BGR（本地相机）；取消则为平面 RGB", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBox references = new() { AcceptsReturn = true, Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 4, 0, 10) };
    private readonly ListBox files = new() { Height = 140, Margin = new Thickness(0, 6, 0, 10) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
    private readonly Button generate = new() { Content = "生成并保存", Padding = new Thickness(12, 6, 12, 6) };
    private readonly Button import = new() { Content = "导入 CVRAW…", Padding = new Thickness(12, 6, 12, 6) };
    private readonly Button cancel = new() { Content = "关闭", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(10, 0, 0, 0) };
    private readonly TextBlock description = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 14) };
    private readonly TextBlock radiusLabel = new();
    private readonly TextBlock thresholdLabel = new() { Text = "缺陷阈值（原始 DN 值）" };
    private readonly TextBlock referenceLabel = new() { TextWrapping = TextWrapping.Wrap };
    private string[] paths = Array.Empty<string>();
    private CancellationTokenSource? cancellation;
    public string? CreatedFilePath { get; private set; }
    public CalibrationType CreatedType { get; private set; }
    public static bool Supports(CalibrationType value) => value is CalibrationType.DSNU or CalibrationType.Uniformity or CalibrationType.DefectPoint or CalibrationType.DefectWPoint or CalibrationType.DefectBPoint or CalibrationType.Luminance or CalibrationType.LumOneColor or CalibrationType.LumFourColor or CalibrationType.LumMultiColor;
    public static bool SupportsType(CalibrationType value) => Supports(value) || value is CalibrationType.Distortion or CalibrationType.ColorShift;

    public CalibrationCreationWindow(CalibrationType initialType)
    {
        Title = "从原始图像创建校正"; Width = 640; Height = 700; MinWidth = 500; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/UpdateDialogTheme.xaml", UriKind.Relative) });
        SetResourceReference(BackgroundProperty, "UpdateDialog.Background"); SetResourceReference(ForegroundProperty, "UpdateDialog.TextPrimary");
        this.ApplyCaption();
        import.SetResourceReference(StyleProperty, "UpdateDialogSecondaryButtonStyle");
        cancel.SetResourceReference(StyleProperty, "UpdateDialogSecondaryButtonStyle");
        generate.SetResourceReference(StyleProperty, "UpdateDialogPrimaryButtonStyle");
        StackPanel panel = new() { Margin = new Thickness(20) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "创建校正", FontSize = 25, FontWeight = FontWeights.SemiBold }); panel.Children.Add(description);
        type.Items.Add(new ComboBoxItem { Content = "DSNU 暗场平均", Tag = CalibrationType.DSNU });
        type.Items.Add(new ComboBoxItem { Content = "均匀场（中心亮度 / 像素亮度）", Tag = CalibrationType.Uniformity });
        type.Items.Add(new ComboBoxItem { Content = "缺陷点（任一通道超出阈值）", Tag = CalibrationType.DefectPoint });
        type.Items.Add(new ComboBoxItem { Content = "亮度（单通道）", Tag = CalibrationType.Luminance });
        type.Items.Add(new ComboBoxItem { Content = "单色（X= aR+dB，Y=bG，Z=cB）", Tag = CalibrationType.LumOneColor });
        type.Items.Add(new ComboBoxItem { Content = "四色（RGB 到 XYZ 矩阵）", Tag = CalibrationType.LumFourColor });
        type.Items.Add(new ComboBoxItem { Content = "多色（RGB 到 XYZ 矩阵）", Tag = CalibrationType.LumMultiColor });
        type.Items.Add(new ComboBoxItem { Content = "畸变（普通镜头棋盘标定）", Tag = CalibrationType.Distortion });
        type.Items.Add(new ComboBoxItem { Content = "色偏（RGB 棋盘对齐到 G）", Tag = CalibrationType.ColorShift });
        type.SelectedIndex = initialType == CalibrationType.Uniformity ? 1 : initialType is CalibrationType.DefectPoint or CalibrationType.DefectWPoint or CalibrationType.DefectBPoint ? 2 : 0;
        for (int i = 3; i < type.Items.Count; i++) if ((CalibrationType)((ComboBoxItem)type.Items[i]).Tag == initialType) type.SelectedIndex = i;
        bright.IsChecked = initialType != CalibrationType.DefectBPoint;
        panel.Children.Add(type); panel.Children.Add(import); panel.Children.Add(files); panel.Children.Add(rawConfirmed); panel.Children.Add(interleaved);
        panel.Children.Add(radiusLabel); panel.Children.Add(radius);
        panel.Children.Add(thresholdLabel); panel.Children.Add(threshold); panel.Children.Add(bright);
        panel.Children.Add(referenceLabel); panel.Children.Add(references);
        panel.Children.Add(status); StackPanel actions = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; actions.Children.Add(generate); actions.Children.Add(cancel); panel.Children.Add(actions);
        import.Click += (_, _) => { bool chessboard = (CalibrationType)((ComboBoxItem)type.SelectedItem).Tag == CalibrationType.Distortion; OpenFileDialog dialog = new() { Filter = chessboard ? "棋盘图像 (*.png;*.tif;*.tiff;*.bmp;*.jpg)|*.png;*.tif;*.tiff;*.bmp;*.jpg" : "原始 CVRAW (*.cvraw)|*.cvraw", Multiselect = true }; if (dialog.ShowDialog(this) == true) { paths = dialog.FileNames; files.ItemsSource = paths; status.Text = $"已导入 {paths.Length} 张图像"; } };
        generate.Click += Generate;
        cancel.Click += (_, _) => { if (cancellation != null) { cancellation.Cancel(); status.Text = "正在取消，请等待当前步骤结束。"; } else Close(); };
        Closing += (_, args) => { if (cancellation != null) { cancellation.Cancel(); args.Cancel = true; status.Text = "正在取消，请等待读取结束。"; } };
        type.SelectionChanged += (_, _) => { paths = Array.Empty<string>(); files.ItemsSource = paths; references.Clear(); rawConfirmed.IsChecked = false; status.Text = "请导入适用于当前校正的图像。"; UpdateFields(); }; UpdateFields();
    }

    private void UpdateFields()
    {
        CalibrationType selected = (CalibrationType)((ComboBoxItem)type.SelectedItem).Tag;
        bool colorShift = selected == CalibrationType.ColorShift;
        references.IsEnabled = selected is CalibrationType.Luminance or CalibrationType.LumOneColor or CalibrationType.LumFourColor or CalibrationType.LumMultiColor or CalibrationType.Distortion or CalibrationType.ColorShift;
        radius.IsEnabled = selected == CalibrationType.Uniformity || references.IsEnabled && selected != CalibrationType.Distortion && !colorShift; threshold.IsEnabled = selected == CalibrationType.DefectPoint; bright.IsEnabled = threshold.IsEnabled;
        import.Content = selected == CalibrationType.Distortion ? "导入棋盘图像…" : "导入 CVRAW…";
        bool distortion = selected == CalibrationType.Distortion;
        radius.Visibility = radiusLabel.Visibility = radius.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        threshold.Visibility = thresholdLabel.Visibility = bright.Visibility = threshold.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        references.Visibility = referenceLabel.Visibility = references.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        interleaved.Visibility = distortion ? Visibility.Collapsed : Visibility.Visible;
        radiusLabel.Text = selected == CalibrationType.Uniformity ? "中心亮度参考区域半径（像素，方形区域）" : "测量 ROI 半径（像素，方形区域）";
        referenceLabel.Text = distortion ? "棋盘参数：内角点列数,行数,格距，例如 9,6,1（英文逗号）" : selected == CalibrationType.LumOneColor ? "按文件顺序每张填写一行参考 Y,x,y（英文逗号）。至少两个红/蓝响应线性独立的样本，用于拟合 a、d；曝光取文件元数据。" : "按文件顺序每张填写一行参考 Y,x,y（英文逗号）。四色/多色至少三个独立颜色，四色建议白/红/绿/蓝。曝光取文件元数据。";
        if (selected == CalibrationType.Luminance) referenceLabel.Text = "按文件顺序，每张填写一行参考亮度 Y（单个数字）；曝光取文件元数据。";
        if (colorShift) referenceLabel.Text = "内角点列数,行数,最大允许残差（像素），例如 9,6,1。行列须一奇一偶；残差上限须按实际用途填写。";
        rawConfirmed.Content = distortion ? "我确认这些棋盘图像没有经过畸变校正" : "我确认采集这些图像时已关闭全部校正";
        description.Text = distortion ? "普通镜头标定：导入至少五张不同姿态、覆盖画面各区域的棋盘图像。输出相机矩阵及畸变系数。" : selected == CalibrationType.DSNU ? "导入遮光暗场 CVRAW，在同一相机、曝光和增益下求平均，生成暗场扣除地图。" : selected == CalibrationType.Uniformity ? "导入均匀亮场 CVRAW，使用中心参考亮度除以各像素亮度生成增益地图。" : selected == CalibrationType.DefectPoint ? "导入暗场检测亮缺陷，或导入亮场检测暗缺陷；任一通道超过阈值时记录该像素。" : "导入同一相机、尺寸和增益下的未校正 CVRAW，输入对应参考 Y,x,y，拟合亮度或色度系数。";
        if (selected == CalibrationType.Luminance) description.Text = "导入单通道未校正 CVRAW，输入每张图像的参考亮度 Y，拟合曝光归一化后的亮度系数。";
        if (colorShift) description.Text = "导入至少三张独立拍摄、覆盖不同区域的 RGB 棋盘 CVRAW。最后一张仅验证，其余用于拟合。保持相同相机、曝光、增益和光学设置；通道偏移须小于棋盘最小点距的一半。只校正整数平移，边缘填黑；径向色差或错配超出残差上限时拒绝生成。";
    }

    private async void Generate(object sender, RoutedEventArgs e)
    {
        string? temporary = null;
        List<FileStream> sourceLocks = new();
        try
        {
            if (paths.Length == 0) throw new InvalidOperationException("请先导入图像文件。");
            if (rawConfirmed.IsChecked != true) throw new InvalidOperationException("请确认输入图像在采集时关闭了全部校正。文件本身无法证明全部采集设置。");
            CalibrationType selected = (CalibrationType)((ComboBoxItem)type.SelectedItem).Tag;
            int centerRadius = 50; double limit = 100;
            bool distortion = selected == CalibrationType.Distortion;
            bool colorShift = selected == CalibrationType.ColorShift;
            bool color = references.IsEnabled && !distortion && !colorShift;
            if ((selected == CalibrationType.Uniformity || color) && (!int.TryParse(radius.Text, out centerRadius) || centerRadius <= 0)) throw new InvalidOperationException("中心半径必须是正整数。");
            if (selected == CalibrationType.DefectPoint && !double.TryParse(threshold.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out limit)) throw new InvalidOperationException("缺陷阈值必须是数值。");
            bool detectBright = bright.IsChecked == true;
            bool interleavedBgr = interleaved.IsChecked == true;
            string referencesText = references.Text;
            string[] referenceLines = referencesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (color && referenceLines.Length != paths.Length) throw new InvalidOperationException(selected == CalibrationType.Luminance ? "每张输入图像须有一行对应的参考亮度 Y。" : "每张输入图像须有一行对应的参考 Y,x,y。");
            SaveFileDialog dialog = new() { Filter = color || distortion || colorShift ? "校正 JSON (*.json)|*.json" : selected == CalibrationType.DSNU ? "DSNU (*.u16map)|*.u16map" : selected == CalibrationType.Uniformity ? "均匀场 (*.f32map)|*.f32map" : "缺陷点 (*.dat)|*.dat", FileName = selected.ToString(), OverwritePrompt = false };
            if (dialog.ShowDialog(this) != true) return;
            if (File.Exists(dialog.FileName)) throw new IOException("所选输出文件已经存在，请选择新的文件名。创建校正不会覆盖已有文件。");
            foreach (string path in paths) if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(dialog.FileName), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("输出文件不能覆盖原始输入图像。");
            // Deny source writes/deletes until generation and the create-only move finish.
            foreach (string path in paths) sourceLocks.Add(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
            (long Length, DateTime Modified)[] inputs = new (long, DateTime)[paths.Length];
            for (int i = 0; i < paths.Length; i++) { FileInfo input = new(paths[i]); inputs[i] = (input.Length, input.LastWriteTimeUtc); }
            double? fittedError = null;
            double? distortionError = null;
            CalibrationColorShiftFit? shiftFit = null;
            temporary = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string tempPath = temporary;
            cancellation = new(); CancellationToken token = cancellation.Token;
            import.IsEnabled = generate.IsEnabled = type.IsEnabled = false;
            cancel.Content = "取消生成";
            Progress<string> progress = new(text => status.Text = text);
            await Task.Run(() =>
            {
                if (colorShift)
                {
                    string[] fields = referencesText.Split(',');
                    if (fields.Length != 3 || !int.TryParse(fields[0], out int columns) || !int.TryParse(fields[1], out int rows) || !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double residual))
                        throw new InvalidOperationException("色偏参数须为内角点列数,行数,最大允许残差（像素），如 9,6,1。");
                    shiftFit = CalibrationColorShiftCreation.Fit(paths, columns, rows, residual, interleavedBgr, token, progress);
                    File.WriteAllText(tempPath, shiftFit.Json.ToString(), new System.Text.UTF8Encoding(false));
                    token.ThrowIfCancellationRequested(); return;
                }
                if (distortion)
                {
                    string[] fields = referencesText.Split(',');
                    if (fields.Length != 3 || !int.TryParse(fields[0], out int columns) || !int.TryParse(fields[1], out int rows) || !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double squareSize)) throw new InvalidOperationException("棋盘参数须为内角点列数,行数,格距，如 9,6,1。");
                    CalibrationDistortionFit fit = CalibrationDistortionCreation.Fit(paths, columns, rows, squareSize, token, progress);
                    File.WriteAllText(tempPath, fit.Json.ToString(), new System.Text.UTF8Encoding(false)); distortionError = fit.ReprojectionRmsPixels; return;
                }
                if (color)
                {
                    List<CalibrationColorSample> samples = new(); int bpp = 0, width = 0, height = 0; float? gain = null;
                    foreach (string path in paths)
                    {
                        string[] fields = referenceLines[samples.Count].Split(',');
                        double luminance, x = double.NaN, y = double.NaN;
                        if (selected == CalibrationType.Luminance)
                        {
                            if (fields.Length != 1 || !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out luminance)) throw new InvalidOperationException("亮度参考值每行须为一个数字 Y，使用英文小数点。");
                        }
                        else if (fields.Length != 3 || !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out luminance) || !double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) throw new InvalidOperationException("色度参考值每行须为三个数字 Y,x,y，使用英文小数点和逗号。");
                        if (ColorVision.FileIO.CVFileUtil.ReadCIEFileHeader(path, out ColorVision.FileIO.CVCIEFile header) < 0) throw new InvalidDataException("CVRAW 头无效。");
                        using (header)
                        {
                            if (bpp != 0 && (bpp != header.Bpp || width != header.Cols || height != header.Rows || gain != header.Gain)) throw new InvalidDataException("所有色度样本的位深、尺寸和采集增益须相同。");
                            bpp = header.Bpp; width = header.Cols; height = header.Rows; gain = header.Gain;
                        }
                        samples.Add(CalibrationColorCreation.ReadSample(path, luminance, x, y, centerRadius, interleavedBgr, token));
                        ((IProgress<string>)progress).Report($"已读取色度样本 {samples.Count}/{paths.Length}");
                    }
                    CalibrationColorFit fit = CalibrationColorCreation.Fit(samples, selected, bpp);
                    if (!double.IsFinite(fit.RelativeRmsError)) throw new InvalidDataException("拟合误差无效。");
                    File.WriteAllText(tempPath, fit.Json.ToString(), new System.Text.UTF8Encoding(false));
                    fittedError = fit.RelativeRmsError;
                    ((IProgress<string>)progress).Report($"拟合相对 RMS 误差：{fit.RelativeRmsError:P2}");
                    token.ThrowIfCancellationRequested(); return;
                }
                CalibrationRawAverage average = CalibrationMapCreation.Average(paths, token, progress, interleavedBgr);
                using FileStream output = new(tempPath, FileMode.CreateNew, FileAccess.Write);
                if (selected == CalibrationType.DefectPoint) CalibrationMapCreation.WriteDefects(output, average, limit, detectBright, token);
                else CalibrationMapCreation.WriteMap(output, average, selected, centerRadius, token);
                output.Flush(true); token.ThrowIfCancellationRequested();
            }, token);
            token.ThrowIfCancellationRequested();
            for (int i = 0; i < paths.Length; i++)
            {
                FileInfo input = new(paths[i]);
                if (!input.Exists || input.Length != inputs[i].Length || input.LastWriteTimeUtc != inputs[i].Modified) throw new IOException("生成过程中输入文件发生变化，请重新导入。");
            }
            if (File.Exists(dialog.FileName)) throw new IOException("输出文件已被其他操作创建，请选择新的文件名。");
            File.Move(tempPath, dialog.FileName, false); temporary = null;
            CreatedFilePath = dialog.FileName; CreatedType = selected;
            cancellation.Dispose(); cancellation = null;
            if (fittedError.HasValue) MessageBox.Show(this, $"已保存校正。样本相对 RMS 误差：{fittedError.Value:P2}。请使用独立参考样本验证实际测量精度。", "色度拟合结果", MessageBoxButton.OK, MessageBoxImage.Information);
            if (distortionError.HasValue) MessageBox.Show(this, $"已保存普通镜头畸变校正。重投影 RMS：{distortionError.Value:F3} 像素。请检查棋盘覆盖范围及独立图像校正效果。", "畸变拟合结果", MessageBoxButton.OK, MessageBoxImage.Information);
            if (shiftFit != null) MessageBox.Show(this, $"已保存色偏整数平移。拟合 RMS：{shiftFit.Training.RmsPixels:F3} 像素。\n最后一张独立验证：RMS {shiftFit.BeforeValidation.RmsPixels:F3} → {shiftFit.Validation.RmsPixels:F3} 像素，最大残差 {shiftFit.Validation.MaximumPixels:F3} 像素。\n误差来自棋盘点，未验收真实设备精度或边缘填充效果。", "色偏拟合结果", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (OperationCanceledException) { status.Text = "已取消生成。"; }
        catch (Exception exception) { status.Text = exception.Message; MessageBox.Show(this, exception.Message, "校正生成失败", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally
        {
            foreach (FileStream source in sourceLocks) source.Dispose();
            cancellation?.Dispose(); cancellation = null;
            if (temporary != null && File.Exists(temporary))
            {
                try { File.Delete(temporary); }
                catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException) { status.Text = $"临时文件未能删除：{temporary}\n{cleanupError.Message}"; }
            }
            import.IsEnabled = generate.IsEnabled = type.IsEnabled = true; UpdateFields();
            cancel.Content = "关闭";
        }
    }
}

