using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using ColorVision.UI;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2;

public partial class TemplateMatchingWindow : Window
{
    private TemplateMatchingDocument document = new();
    private ImageProcessingContext? imageContext;
    private DrawEditorContext? drawContext;
    private ImageViewConfig? imageConfig;
    private RoiRect searchRegion;
    private long sourceRevision;
    private bool closed;
    private bool running;
    private bool isNodeEditor;
    private P2ResultOverlayVisual? overlay;
    public string SelectedPath { get; private set; } = string.Empty;
    public string ResultJson { get; private set; } = string.Empty;

    public static TemplateMatchingWindow ForNode(string parameterJson)
    {
        TemplateMatchingDocument draft = string.IsNullOrWhiteSpace(parameterJson) ? new() : TemplateMatchingDocument.FromJson(parameterJson);
        TemplateMatchingWindow window = new()
        {
            isNodeEditor = true,
            document = draft
        };
        window.ApplyButton.Content = DisplayText.Get("应用到节点");
        window.ExportButton.Visibility = window.CancelButton.Visibility = Visibility.Visible;
        window.ShowDocument();
        return window;
    }

    public TemplateMatchingWindow(string? templatePath = null)
    {
        InitializeComponent();
        this.ApplyCaption();
        if (!string.IsNullOrWhiteSpace(templatePath)) LoadTemplate(templatePath);
        else ShowDocument();
        Closed += (_, _) => { closed = true; ClearResult(); };
    }

    internal TemplateMatchingWindow(ImageProcessingContext context, DrawEditorContext drawings, ImageViewConfig config,
        RoiRect roi, TemplateMatchingImage? reference) : this()
    {
        imageContext = context;
        drawContext = drawings;
        imageConfig = config;
        sourceRevision = context.ImageRevision;
        searchRegion = roi;
        if (reference != null) document.Image = reference;
        ShowDocument();
        RunButton.IsEnabled = true;
    }

    private void ShowDocument()
    {
        NameText.Text = document.Name;
        ParameterEditor.Content = PropertyEditorHelper.GenPropertyEditorControl(document.Options);
        TemplatePreview.Source = document.Image.Pixels.Length == 0 ? null : document.Image.CreatePreview();
        ImageInfo.Text = document.Image.Pixels.Length == 0 ? DisplayText.Get("导入参考图，或在图像中框选目标并设为模板。") :
            DisplayText.Format($"{document.Image.Width} × {document.Image.Height} · {document.Image.Depth} 位 · {document.Image.Channels} 通道");
        FileText.Text = isNodeEditor ? DisplayText.Get("参考图与参数随流程节点保存") : string.IsNullOrEmpty(SelectedPath) ? DisplayText.Get("尚未导出") : SelectedPath;
        SearchInfo.Text = imageContext == null ? DisplayText.Get("在图像编辑器中打开模板匹配，可对当前图像试运行。") : DisplayText.Format($"当前图像 · 搜索区域：{P2RoiHelper.Describe(searchRegion)}");
        StatusText.Text = isNodeEditor ? DisplayText.Get("编辑完成后点击“应用到节点”，再保存流程；取消或关闭不会修改节点。") : DisplayText.Get("可导入参考图进行试匹配；导出 .cvtm 后可在流程节点中导入使用。");
    }

    internal void LoadTemplate(string path)
    {
        TemplateMatchingDocument loaded = TemplateMatchingFile.Load(path);
        // Decode the preview before replacing the current document.
        _ = loaded.Image.CreatePreview();
        document = loaded;
        SelectedPath = Path.GetFullPath(path);
        ClearResult();
        ShowDocument();
    }

    private TemplateMatchingDocument Snapshot()
    {
        CommitEditor(ParameterEditor);
        TemplateMatchingDocument snapshot = new() { Name = NameText.Text.Trim(), Image = document.Image, Options = document.Options.Copy() };
        snapshot.Validate();
        return snapshot;
    }

    private static void CommitEditor(DependencyObject control)
    {
        if (control is TextBox textBox) textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (Validation.GetHasError(control)) throw new InvalidOperationException(DisplayText.Get("请修正参数中的错误输入。"));
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(control); i++) CommitEditor(VisualTreeHelper.GetChild(control, i));
    }

    private void Load_Click(object sender, RoutedEventArgs e) => TryAction(() =>
    {
        OpenFileDialog dialog = new() { Filter = TemplateMatchingFile.Filter, InitialDirectory = ExistingDirectory() };
        if (dialog.ShowDialog(this) == true) LoadTemplate(dialog.FileName);
    });

    private void New_Click(object sender, RoutedEventArgs e)
    {
        document = new();
        SelectedPath = string.Empty;
        ClearResult();
        ShowDocument();
    }

    private void Import_Click(object sender, RoutedEventArgs e) => TryAction(() =>
    {
        OpenFileDialog dialog = new() { Filter = DisplayText.Get("图像文件|*.png;*.tif;*.tiff;*.bmp;*.jpg;*.jpeg|所有文件|*.*") };
        if (dialog.ShowDialog(this) != true) return;
        using P2ImageSnapshot source = P2ImageSnapshot.FromBitmap(P2BitmapLoader.Load(dialog.FileName));
        TemplateMatchingImage reference = TemplateMatchingImage.Capture(source.Image, default);
        document.Name = NameText.Text.Trim();
        document.Image = reference;
        ClearResult();
        ShowDocument();
    });

    private void Save_Click(object sender, RoutedEventArgs e) => TryAction(() =>
    {
        if (!isNodeEditor) { Export(); return; }
        ResultJson = Snapshot().ToJson();
        DialogResult = true;
    });
    private void Export_Click(object sender, RoutedEventArgs e) => TryAction(Export);

    private void Export()
    {
        TemplateMatchingDocument snapshot = Snapshot();
        Directory.CreateDirectory(TemplateMatchingFile.DefaultDirectory);
        SaveFileDialog dialog = new() { Filter = TemplateMatchingFile.Filter, DefaultExt = TemplateMatchingFile.Extension,
            AddExtension = true, OverwritePrompt = true, InitialDirectory = ExistingDirectory(), FileName = Path.GetFileName(SelectedPath) };
        if (dialog.ShowDialog(this) != true) return;
        string path = dialog.FileName;
        TemplateMatchingFile.Save(path, snapshot);
        document.Name = snapshot.Name;
        SelectedPath = Path.GetFullPath(path);
        if (!isNodeEditor) FileText.Text = SelectedPath;
        StatusText.Text = isNodeEditor ? DisplayText.Get("模板已导出。点击“应用到节点”才会更新当前节点。") : DisplayText.Get("模板已导出，可在本地模板匹配节点中导入。");
    }

    private string ExistingDirectory() => !string.IsNullOrEmpty(SelectedPath) ? Path.GetDirectoryName(SelectedPath)! : TemplateMatchingFile.DefaultDirectory;

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        document.Name = NameText.Text.Trim();
        document.Options = new();
        ClearResult();
        ShowDocument();
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (running || imageContext == null) return;
        ClearResult();
        try
        {
            TemplateMatchingDocument snapshot = Snapshot();
            if (!imageContext.IsCurrentImageRevision(sourceRevision)) throw new InvalidOperationException(DisplayText.Get("源图已变化，请重新打开模板匹配窗口以核对搜索区域。"));
            using ImageFrameLease lease = imageContext.AcquireImageFrame() ?? throw new InvalidOperationException(DisplayText.Get("没有可用源图像。"));
            running = true;
            SetEditingEnabled(false);
            StatusText.Text = DisplayText.Get("正在匹配…");
            TemplateMatchingResult result = await Task.Run(() => TemplateMatching.Run(lease.Image, snapshot, searchRegion));
            if (closed) return;
            if (!imageContext.IsCurrentImageRevision(sourceRevision)) throw new InvalidOperationException(DisplayText.Get("计算期间源图已变化，结果已丢弃。"));
            ResultText.Text = result.RawJson;
            StatusText.Text = $"{(result.Success ? DisplayText.Get("成功") : DisplayText.Get("未匹配"))} · {result.StatusCode} · {result.Message} {string.Join("；", result.Warnings)}";
            if (!result.Success) return;
            MatchesGrid.ItemsSource = result.Matches;
            overlay = new P2ResultOverlayVisual(JObject.Parse(result.RawJson), P2OverlayKind.TemplateMatching, imageConfig!);
            overlay.ApplyLayoutScale(new DrawingVisualScaleContext(drawContext!.DrawCanvas.IsLayoutUpdated, drawContext.DrawCanvas.Scale, drawContext.DrawCanvas.TextFontSizeOverride));
            drawContext.DrawCanvas.AddOverlayVisual(overlay);
        }
        catch (Exception ex) { if (!closed) { ClearResult(); StatusText.Text = ex.Message; } }
        finally { running = false; if (!closed) SetEditingEnabled(true); }
    }

    private void SetEditingEnabled(bool enabled)
    {
        FileActions.IsEnabled = ParameterEditor.IsEnabled = NameText.IsEnabled = enabled;
        RunButton.IsEnabled = enabled && imageContext != null;
    }

    private void Clear_Click(object sender, RoutedEventArgs e) { if (!running) ClearResult(); }
    private void ClearResult()
    {
        if (overlay != null) drawContext?.DrawCanvas.RemoveOverlayVisual(overlay);
        overlay = null;
        MatchesGrid.ItemsSource = null;
        ResultText.Clear();
    }

    private void TryAction(Action action)
    {
        try { action(); }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
