using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Images.FileFusion;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.FlowProcessing.Nodes;

public enum FileFusionInputMode { Folder, FileList }

// STN's default string[] descriptor uses commas as separators, which corrupts valid file names.
public sealed class FusionFileListDescriptor : STNodePropertyDescriptor
{
    protected override string GetStringFromValue(bool isLang = false) => JsonConvert.SerializeObject(PropertyInfo.GetValue(Node));
    protected override object GetValueFromString(string strText) => JsonConvert.DeserializeObject<string[]>(strText) ?? Array.Empty<string>();
}

internal sealed record LocalFileFusionResultData(int MasterId, int MasterResultType, string MasterValue,
    string FrameId, string ImageFilePath, int ImageCount, string ActualMode, long TotalTime);

[STNode("Flow_CustomNodes", "景深融合")]
public sealed class LocalFileFusionNode : LocalFlowNodeBase
{
    private readonly IFileFusionServices services;
    private FileFusionInputMode inputMode;
    private string inputDirectory = string.Empty;
    private string[] files = Array.Empty<string>();
    private FileFusionMode mode;
    private string resultDirectory = string.Empty;

    [Category("景深融合")]
    [STNodeProperty("输入方式", "读取文件夹中的图片，或使用按焦点顺序排列的文件列表。", true)]
    public FileFusionInputMode InputMode { get => inputMode; set { inputMode = value; OnPropertyChanged(); } }

    [Category("景深融合")]
    [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
    [PropertyVisibility(nameof(InputMode), FileFusionInputMode.Folder)]
    [STNodeProperty("输入文件夹", "只读取直接子文件，按文件名自然排序；不递归子文件夹。", true)]
    public string InputDirectory { get => inputDirectory; set { inputDirectory = value ?? string.Empty; OnPropertyChanged(); } }

    [Category("景深融合")]
    [PropertyEditorType(typeof(CollectionJsonEditor))]
    [CollectionEditorType(typeof(TextSelectFilePropertiesEditor))]
    [PropertyVisibility(nameof(InputMode), FileFusionInputMode.FileList)]
    [STNodeProperty("有序文件列表", "使用列表顺序；至少两张同尺寸、同通道的 8-bit 图片。", true, DescriptorType = typeof(FusionFileListDescriptor))]
    public string[] Files { get => files; set { files = value ?? Array.Empty<string>(); OnPropertyChanged(); } }

    [Category("景深融合")]
    [STNodeProperty("计算模式", "自动模式对 2–4 张图片使用 CPU；强制 GPU 至少需要五张。", true)]
    public FileFusionMode Mode { get => mode; set { mode = value; OnPropertyChanged(); } }

    [Category("景深融合")]
    [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
    [STNodeProperty("结果目录", "留空保存到 LocalAppData/ColorVision/Results/Fusion；不得与输入图片所在目录相同。", true)]
    public string ResultDirectory { get => resultDirectory; set { resultDirectory = value ?? string.Empty; OnPropertyChanged(); } }

    public LocalFileFusionNode() : this(FileFusionServices.Instance) { }

    internal LocalFileFusionNode(IFileFusionServices services) : base("景深融合", "LocalFileFusion", "Fusion")
    {
        this.services = services;
    }

    protected override string GetCompactSummaryValue() => InputMode == FileFusionInputMode.Folder
        ? CompactValueOrDash(Path.GetFileName(InputDirectory)) : $"{Files.Length} 张 · {Mode}";

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action) => new() { Data = ExecuteSynchronously(action) };

    internal LocalFileFusionResultData ExecuteSynchronously(CVStartCFC action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!Enum.IsDefined(InputMode)) throw new InvalidOperationException("无效的融合输入方式。");
        string[] sourceFiles = InputMode == FileFusionInputMode.Folder ? FileFusion.GetFolderFiles(InputDirectory) : Files.ToArray();
        FileFusionMode selectedMode = Mode;
        string directory = ResolveOutputDirectory(ResultDirectory, sourceFiles);
        string nodeId = NodeID;
        int zIndex = ZIndex;
        using var stop = new FusionRunCancellation();
        string runKey = $"ColorVision.FileFusion.{Guid.NewGuid():N}";
        LocalFlowFrame? frame = null;
        try
        {
            action.RuntimeResources.Set(runKey, stop);
            EnsureRunning(action, stop.Token);
            int batchId = FlowNodeTiming.Run("ResolveBatch", () => services.ResolveBatchId(action.SerialNumber));
            FileFusionResult fusion = FlowNodeTiming.Run("Fusion", () => services.Execute(sourceFiles, selectedMode, stop.Token));
            EnsureRunning(action, stop.Token);
            string outputPath = FlowNodeTiming.Run("SaveImage", () => SaveImage(fusion.Image, directory));
            EnsureRunning(action, stop.Token);
            frame = FlowNodeTiming.Run("OpenImage", () => LocalFrameFileService.Load(outputPath));
            var model = new MeasureResultImgModel
            {
                BatchId = batchId, ZIndex = zIndex, NDPort = -1, DeviceCode = null,
                RawFile = Path.GetFileName(outputPath), FileUrl = outputPath, FileType = 0,
                ResultCode = 0, Result = "ok", CreateDate = DateTime.Now,
                TotalTime = (int)Math.Min(fusion.TotalMs, int.MaxValue),
                ImgFrameInfo = JsonConvert.SerializeObject(new { bpp = 8, width = frame.Metadata.Width, height = frame.Metadata.Height, channels = frame.Metadata.Channels }),
                Params = JsonConvert.SerializeObject(new
                {
                    Algorithm = "FileFusion", Bpp = 8, Gain = 0, IsHDR = false, NDPort = -1,
                    ExpTime = new int[3], AvgCount = 1, FlipMode = -99,
                    RequestedMode = selectedMode.ToString(), ActualMode = fusion.ActualMode.ToString(),
                    InputFiles = fusion.Files, fusion.ValidationMs, fusion.NativeMs, fusion.ConvertMs, fusion.TotalMs,
                }),
            };
            EnsureRunning(action, stop.Token);
            int masterId = FlowNodeTiming.Run("PersistResult", () => services.SaveResult(model));
            if (masterId <= 0) throw new InvalidOperationException("保存景深融合图像记录失败。");
            model.Id = masterId;
            frame.MasterId = masterId;
            EnsureRunning(action, stop.Token);
            action.SetCurrentFrame(frame);
            string frameId = frame.FrameId.ToString("N");
            frame = null; // RuntimeResources owns the frame from this point.
            EnsureRunning(action, stop.Token);
            FlowNodeTiming.Run("PublishResult", () => services.Publish(action, nodeId, zIndex, model));
            EnsureRunning(action, stop.Token);
            action.MasterValue(outputPath, masterId, 100);
            return new(masterId, 100, outputPath, frameId, outputPath, fusion.Files.Count, fusion.ActualMode.ToString(), fusion.TotalMs);
        }
        finally
        {
            frame?.Dispose();
            action.RuntimeResources.Remove(runKey);
            stop.Complete();
        }
    }

    internal static string ResolveOutputDirectory(string configuredDirectory, IReadOnlyList<string> sourceFiles)
    {
        string directory = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Results", "Fusion")
            : configuredDirectory);
        directory = Path.TrimEndingDirectorySeparator(directory);
        if (sourceFiles.Any(file => string.Equals(Path.GetDirectoryName(Path.GetFullPath(file)), directory, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("融合结果目录不能与输入图片目录相同，避免下次将结果再次作为输入。");
        return directory;
    }

    private static string SaveImage(BitmapSource image, string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"Fusion_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(stream);
        return path;
    }

    private static void EnsureRunning(CVStartCFC action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (action.RuntimeResources.IsDisposed || action.TryGetStopStatus(out _)) throw new OperationCanceledException("流程已停止。", token);
    }

    private sealed class FusionRunCancellation : IDisposable
    {
        private readonly object sync = new();
        private readonly CancellationTokenSource source = new();
        private bool completed;
        public CancellationToken Token => source.Token;
        public void Dispose() { lock (sync) { if (!completed) source.Cancel(); } }
        public void Complete() { lock (sync) { completed = true; source.Dispose(); } }
    }
}
