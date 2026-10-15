using ColorVision.Core;

namespace ColorVision.Engine.Services.Algorithms;

internal interface ILocalTemplateMatchingServices
{
    TemplateMatchingResult Detect(HImage image, TemplateMatchingDocument document, RoiRect roi);
}

internal sealed class LocalTemplateMatchingService : ILocalTemplateMatchingServices
{
    public static LocalTemplateMatchingService Instance { get; } = new();
    public TemplateMatchingResult Detect(HImage image, TemplateMatchingDocument document, RoiRect roi) => TemplateMatching.Run(image, document, roi);
}
