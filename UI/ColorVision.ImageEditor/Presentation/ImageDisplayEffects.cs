using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.EditorTools.PseudoColor;
using ColorVision.ImageEditor.Presentation.PseudoColor;
using System;

namespace ColorVision.ImageEditor.Presentation
{
    /// <summary>
    /// Owns the display capabilities of one editor session independently of its tools.
    /// </summary>
    public sealed class ImageDisplayEffects : IDisposable
    {
        private readonly PseudoColorController _pseudoColorController;
        private bool _disposed;

        internal ImageDisplayEffects(ImageProcessingContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            PseudoColor = new PseudoColorState();
            PseudoColor.ApplyDefaults(PseudoColorDefaultConfig.Current);
            _pseudoColorController = new PseudoColorController(context, PseudoColor);
            _pseudoColorController.RefreshPreview();
            Shader = new ImageShaderPresentation(context.Presentation);
        }

        public PseudoColorState PseudoColor { get; }
        public ImageShaderPresentation Shader { get; }

        public bool TryCapturePseudoColorRequest(out PseudoColorFrameRequest request)
            => _pseudoColorController.TryCaptureFrameRequest(out request);

        internal void ConfigureForImage() => _pseudoColorController.ConfigureForImage();

        internal void Invalidate() => _pseudoColorController.Invalidate();

        internal void ResetForSourceChange() => _pseudoColorController.Reset();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _pseudoColorController.Dispose();
            Shader.Dispose();
        }
    }
}
