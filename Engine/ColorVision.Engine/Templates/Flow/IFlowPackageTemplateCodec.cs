namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// Allows a template family with lazy or non-standard persistence to
    /// capture and restore its complete portable value for a cvflow package.
    /// </summary>
    public interface IFlowPackageTemplateCodec
    {
        object CaptureFlowPackageValue(int index);

        bool TryPrepareFlowPackageImport(
            string templateName,
            string serializedContent);
    }
}
