using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.Algorithms;
using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using OpenCvSharp;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing.Nodes;

public abstract class LocalPixelProcessingNode : LocalFlowNodeBase
{
    protected LocalPixelProcessingNode(string title, string nodeType) : base(title, nodeType, nodeType) { }

    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    {
        ServiceName = NodeName, EventName = OperatorCode, action.SerialNumber, Parameters = GetExecutionParameters<object>(action)
    });

    protected LocalNodeExecutionResult Transform(CVStartCFC action, Func<Mat, MatType> outputType,
        Func<Mat, Mat, CancellationToken, object?> transform)
    {
        if (!action.TryAcquireCurrentFrame(out var input) || input == null)
            throw new InvalidOperationException("缺少上游本地图像，请连接相机取图、加载图片或本地图像处理节点。");

        using (input)
        {
            LocalFrameMetadata metadata = input.Metadata;
            if (!input.HasRaw || !input.IsRawFlipApplied)
                throw new InvalidOperationException("需要方向已确定的 8 位或 16 位像素图像。");

            var format = LocalFlowImageAlgorithmAdapter.ResolveRawFormat(metadata.SourceBpp, metadata.Channels);
            int inputDepth = metadata.SourceBpp == 8 ? MatType.CV_8U : MatType.CV_16U;
            int inputStride = checked(metadata.Width * format.BytesPerPixel());
            if (input.RawLength < checked(inputStride * metadata.Height))
                throw new InvalidOperationException("上游图像大小与像素格式不匹配。");

            var cancellationToken = action.RuntimeResources.StopToken;
            cancellationToken.ThrowIfCancellationRequested();
            using Mat source = Mat.FromPixelData(metadata.Height, metadata.Width, MatType.MakeType(inputDepth, metadata.Channels), input.RawPointer, inputStride);
            MatType type = outputType(source);
            int channels = type.Channels;
            int bitsPerChannel = type.Depth == MatType.CV_8U ? 8 : 16;
            int stride = checked(metadata.Width * channels * (bitsPerChannel / 8));

            LocalFlowFrame output = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = metadata.Width,
                Height = metadata.Height,
                Channels = channels,
                SourceBpp = bitsPerChannel,
                PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
                IsMirrorReady = true,
                CaptureTime = metadata.CaptureTime,
            }, checked(stride * metadata.Height), 0);

            try
            {
                using var outputLease = output.Acquire();
                using Mat destination = Mat.FromPixelData(metadata.Height, metadata.Width, type, outputLease.RawPointer, stride);
                object? details = transform(source, destination, cancellationToken);
                if (destination.Data != outputLease.RawPointer)
                    throw new InvalidOperationException("处理后的图像与预期的像素格式不匹配。");
                cancellationToken.ThrowIfCancellationRequested();
                action.SetCurrentFrame(output);
                // The derived pixels have no database record. A legacy consumer must not reopen the source record for them.
                action.Data.Remove("MasterId");
                action.Data.Remove("MasterResultType");
                action.Data.Remove("MasterValue");
                return new LocalNodeExecutionResult
                {
                    Data = new { InputFrameId = input.FrameId, OutputFrameId = output.FrameId, metadata.Width, metadata.Height, BitsPerChannel = bitsPerChannel, Channels = channels, Details = details }
                };
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }
    }
}

[STNode("Flow_LocalAlgorithms", "图像基础调整", CategoryOrder = 9910, NodeOrder = 91)]
[FlowNodeDocumentation("调整数字曝光、亮度、对比度和 Gamma，并输出派生图像。",
    Usage = "将本地图像接到 IN，从 OUT 获取调整后的图像。曝光补偿以 EV 为单位。")]
public sealed class LocalBasicImageAdjustmentNode : LocalPixelProcessingNode
{
    private readonly BasicAdjustmentParameters settings = new();

    [STNodeProperty("曝光补偿 (EV)", "数字曝光补偿，范围 -5 到 5 EV。", true)]
    public double Exposure { get => settings.Exposure; set { settings.Exposure = value; OnPropertyChanged(); } }

    [STNodeProperty("亮度 (%)", "亮度偏移，范围 -100 到 100。", true)]
    public double Brightness { get => settings.Brightness; set { settings.Brightness = value; OnPropertyChanged(); } }

    [STNodeProperty("对比度 (%)", "对比度调整，范围 -100 到 100。", true)]
    public double Contrast { get => settings.Contrast; set { settings.Contrast = value; OnPropertyChanged(); } }

    [STNodeProperty("Gamma", "Gamma，范围 0.1 到 5，默认 1。", true)]
    public double Gamma { get => settings.Gamma; set { settings.Gamma = value; OnPropertyChanged(); } }

    public LocalBasicImageAdjustmentNode() : base("图像基础调整", "LocalBasicImageAdjustment") { }

    protected override object CaptureExecutionParameters(CVStartCFC action) => new BasicAdjustmentParameters
    {
        Exposure = Exposure, Brightness = Brightness, Contrast = Contrast, Gamma = Gamma
    };

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        var parameters = GetExecutionParameters<BasicAdjustmentParameters>(action);
        return Transform(action, static source => source.Type(), (source, destination, token) =>
        {
            ImageToneProcessor.Adjust(source, destination, parameters, token);
            return null;
        });
    }
}

public enum LocalRgbOutputChannel { Rgb, R, G, B, Gray }

[STNode("Flow_LocalAlgorithms", "RGB 通道处理", CategoryOrder = 9910, NodeOrder = 92)]
[FlowNodeDocumentation("对彩色图像应用 R、G、B 增益，可输出彩色图像、单通道或灰度图像。",
    Usage = "将 8 位或 16 位彩色图像接到 IN，设置各通道增益和输出通道。")]
public sealed class LocalRgbChannelProcessingNode : LocalPixelProcessingNode
{
    private sealed record Parameters(WhiteBalanceParameters Gain, LocalRgbOutputChannel OutputChannel);
    private readonly WhiteBalanceParameters gain = new();
    private LocalRgbOutputChannel outputChannel;

    [STNodeProperty("R 增益", "红色通道乘数，范围 0 到 16。", true)]
    public double RedScale { get => gain.RedScale; set { gain.RedScale = value; OnPropertyChanged(); } }

    [STNodeProperty("G 增益", "绿色通道乘数，范围 0 到 16。", true)]
    public double GreenScale { get => gain.GreenScale; set { gain.GreenScale = value; OnPropertyChanged(); } }

    [STNodeProperty("B 增益", "蓝色通道乘数，范围 0 到 16。", true)]
    public double BlueScale { get => gain.BlueScale; set { gain.BlueScale = value; OnPropertyChanged(); } }

    [STNodeProperty("输出通道", "Rgb 输出彩色图像，R/G/B 提取单通道，Gray 按 RGB 转灰度。", true)]
    public LocalRgbOutputChannel OutputChannel { get => outputChannel; set { outputChannel = value; OnPropertyChanged(); } }

    public LocalRgbChannelProcessingNode() : base("RGB 通道处理", "LocalRgbChannelProcessing") { }

    protected override object CaptureExecutionParameters(CVStartCFC action) => new Parameters(
        new WhiteBalanceParameters { RedScale = RedScale, GreenScale = GreenScale, BlueScale = BlueScale }, OutputChannel);

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        var parameters = GetExecutionParameters<Parameters>(action);
        return Transform(action, source =>
        {
            if (source.Channels() is not (3 or 4)) throw new InvalidOperationException("RGB 通道处理需要彩色输入图像。");
            return parameters.OutputChannel == LocalRgbOutputChannel.Rgb ? source.Type() : MatType.MakeType(source.Depth(), 1);
        }, (source, destination, token) =>
        {
            if (parameters.OutputChannel == LocalRgbOutputChannel.Rgb)
            {
                ImageToneProcessor.ScaleRgb(source, destination, parameters.Gain, token);
            }
            else
            {
                ImageColorChannel channel = parameters.OutputChannel switch
                {
                    LocalRgbOutputChannel.R => ImageColorChannel.Red,
                    LocalRgbOutputChannel.G => ImageColorChannel.Green,
                    LocalRgbOutputChannel.B => ImageColorChannel.Blue,
                    LocalRgbOutputChannel.Gray => ImageColorChannel.Gray,
                    _ => throw new InvalidOperationException("未知 RGB 输出通道。")
                };
                if (parameters.Gain.RedScale == 1 && parameters.Gain.GreenScale == 1 && parameters.Gain.BlueScale == 1)
                {
                    ImageChannelProcessor.Select(source, destination, channel, token);
                }
                else
                {
                    using Mat adjusted = new();
                    ImageToneProcessor.ScaleRgb(source, adjusted, parameters.Gain, token);
                    ImageChannelProcessor.Select(adjusted, destination, channel, token);
                }
            }
            return new { parameters.OutputChannel };
        });
    }
}

public enum LocalPseudoColorRangeMode { Auto, Fixed }

[STNode("Flow_LocalAlgorithms", "伪彩色输出", CategoryOrder = 9910, NodeOrder = 93)]
[FlowNodeDocumentation("按自动或固定色标生成伪彩色图像，可在流程中保存 PNG。",
    Usage = "将待着色图像接到 IN。范围默认 Auto；多张图共用色标时选择 Fixed。需要落盘时开启保存 PNG。")]
public sealed class LocalPseudoColorOutputNode : LocalPixelProcessingNode
{
    private sealed record Parameters(ImageColorChannel Channel, StandardPseudoColorMap Colormap,
        LocalPseudoColorRangeMode RangeMode, double Minimum, double Maximum, bool SavePng, string OutputDirectory);
    private ImageColorChannel channel = ImageColorChannel.Gray;
    private StandardPseudoColorMap colormap = StandardPseudoColorMap.Jet;
    private LocalPseudoColorRangeMode rangeMode;
    private double minimum, maximum = 255;
    private bool savePng;
    private string outputDirectory = string.Empty;

    [STNodeProperty("源通道", "Gray 使用灰度值，彩色图像可选择红、绿或蓝通道。", true)]
    public ImageColorChannel Channel { get => channel; set { channel = value; OnPropertyChanged(); } }

    [STNodeProperty("色图", "用于伪彩色映射的色图，默认 Jet。", true)]
    public StandardPseudoColorMap Colormap { get => colormap; set { colormap = value; OnPropertyChanged(); } }

    [STNodeProperty("范围模式", "Auto 按当前图像计算范围；Fixed 使用设置的固定上下限。", true)]
    public LocalPseudoColorRangeMode RangeMode { get => rangeMode; set { rangeMode = value; OnPropertyChanged(); } }

    [PropertyVisibility(nameof(RangeMode), LocalPseudoColorRangeMode.Fixed)]
    [STNodeProperty("固定下限", "输入图像的像素值，必须小于固定上限。", true)]
    public double Minimum { get => minimum; set { minimum = value; OnPropertyChanged(); } }

    [PropertyVisibility(nameof(RangeMode), LocalPseudoColorRangeMode.Fixed)]
    [STNodeProperty("固定上限", "输入图像的像素值，8 位图像通常设为 255，16 位图像可设至 65535。", true)]
    public double Maximum { get => maximum; set { maximum = value; OnPropertyChanged(); } }

    [STNodeProperty("保存 PNG", "每次执行成功后保存一张伪彩图。", true)]
    public bool SavePng { get => savePng; set { savePng = value; OnPropertyChanged(); } }

    [PropertyVisibility(nameof(SavePng))]
    [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
    [STNodeProperty("输出目录", "留空时保存到 %LOCALAPPDATA%\\ColorVision\\Results\\PseudoColor。", true)]
    public string OutputDirectory { get => outputDirectory; set { outputDirectory = value ?? string.Empty; OnPropertyChanged(); } }

    public LocalPseudoColorOutputNode() : base("伪彩色输出", "LocalPseudoColorOutput") { }

    protected override object CaptureExecutionParameters(CVStartCFC action) => new Parameters(Channel, Colormap, RangeMode, Minimum, Maximum, SavePng, OutputDirectory);

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        var parameters = GetExecutionParameters<Parameters>(action);
        if (!Enum.IsDefined(parameters.RangeMode)) throw new InvalidOperationException("未知的伪彩色范围模式。");
        string? pngPath = null;
        var result = Transform(action, static _ => MatType.CV_8UC3, (source, destination, token) =>
        {
            var range = PseudoColorProcessor.RenderSampleRange(source, destination, parameters.Channel, parameters.Colormap,
                parameters.RangeMode == LocalPseudoColorRangeMode.Auto, parameters.Minimum, parameters.Maximum, token);
            if (parameters.SavePng) pngPath = LocalFlowPngOutput.Save(destination, parameters.OutputDirectory, token);
            return new { range.Minimum, range.Maximum, Unit = "DN", PngPath = pngPath };
        });
        if (pngPath == null) action.Data.Remove("LocalPseudoColorPngPath");
        else action.Data["LocalPseudoColorPngPath"] = pngPath;
        return result;
    }
}
