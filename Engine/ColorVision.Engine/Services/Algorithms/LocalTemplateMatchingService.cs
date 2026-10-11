using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;

namespace ColorVision.Engine.Services.Algorithms;

internal interface ILocalTemplateMatchingServices
{
    LocalFlowFrame LoadFrame(string path);
    TemplateMatchingResult Detect(HImage image, TemplateMatchingDocument document, RoiRect roi);
}

internal sealed class LocalTemplateMatchingService : ILocalTemplateMatchingServices
{
    public static LocalTemplateMatchingService Instance { get; } = new();
    public LocalFlowFrame LoadFrame(string path) => LocalFrameFileService.Load(path);
    public TemplateMatchingResult Detect(HImage image, TemplateMatchingDocument document, RoiRect roi) => TemplateMatching.Run(image, document, roi);
}
