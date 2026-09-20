using ColorVision.ImageEditor.Documents;
using ColorVision.ImageEditor.Draw.Ruler;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    /// <summary>Coordinates document mutations and processing lifetime independently of the view's UI.</summary>
    internal sealed class ImageEditorSession : IDisposable
    {
        private readonly ImageDocument _document;
        private readonly Action<long, long> _revisionAdvanced;
        private ImageProcessingContext? _context;
        private bool _suppressConfigurationMutation;
        private bool _disposed;

        internal ImageEditorSession(ImageDocument document, Action<long, long> revisionAdvanced)
        {
            _document = document;
            _revisionAdvanced = revisionAdvanced;
            _document.Changed += OnDocumentChanged;
        }

        private ImageProcessingContext Context => _context
            ?? throw new InvalidOperationException("The editor processing context has not been attached.");

        internal static ImageViewConfig CreateConfiguration()
        {
            ImageViewConfig config = new();
            ImageCalibrationService.ApplyToView(config);
            return config;
        }

        internal void Attach(ImageProcessingContext context)
        {
            if (_context != null) throw new InvalidOperationException("The editor session is already attached.");
            _context = context;
        }

        internal void Invalidate(ImageDocumentMutationKind mutationKind) => _document.Invalidate(mutationKind);

        internal void CommitSourcePixels(ImageSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            _document.AssignSource(source);
            Invalidate(ImageDocumentMutationKind.SourcePixelsChanged);
        }

        // Replacement intentionally clears the previous source before validating the new format.
        // The callback projects the cleared state to the UI at the historical handoff point.
        internal bool TryReplaceSource(ImageSource source, bool enableEditorImageServices, Action sourceCleared)
        {
            ImageProcessingContext context = Context;
            Invalidate(ImageDocumentMutationKind.ImageSourceReplaced);
            context.DisplayEffects.ResetForSourceChange();
            _document.AssignSource(null);
            context.Presentation.Publish(null, null);
            sourceCleared();

            if (!ImageSourceMetadata.TryApply(source, context.Config)) return false;
            if (enableEditorImageServices) ImageCalibrationService.ApplyToView(context.Config);

            _document.AssignSource(source);
            context.Presentation.RestoreSource();
            if (enableEditorImageServices && source is WriteableBitmap) context.DisplayEffects.ConfigureForImage();
            return true;
        }

        internal void OnConfigurationCleared()
        {
            if (!_suppressConfigurationMutation) Invalidate(ImageDocumentMutationKind.SourcePixelsChanged);
            Context.DisplayEffects.ResetForSourceChange();
            Context.Presentation.FunctionImage = null;
        }

        internal void ClearConfiguration()
        {
            bool previousSuppression = _suppressConfigurationMutation;
            _suppressConfigurationMutation = true;
            try
            {
                Context.Config.ClearProperties();
            }
            finally
            {
                _suppressConfigurationMutation = previousSuppression;
            }
        }

        private void OnDocumentChanged(ImageDocumentMutationKind mutationKind, long previousRevision, long currentRevision)
        {
            _context?.Presentation.Invalidate();
            _revisionAdvanced(previousRevision, currentRevision);
            _context?.InvalidateForDocumentMutation(mutationKind, previousRevision, currentRevision);
            _context?.DisplayEffects.Invalidate();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _context?.StreamPresentation.Dispose();
            _context?.DisplayEffects.Dispose();
            _context?.DisposeAlgorithmOverlays();
            _document.Changed -= OnDocumentChanged;
            _document.Dispose();
        }
    }
}
