using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.ImageEditor
{
    public sealed class ImageProcessingContext
    {
        private readonly ImageProcessingContextBinding _binding;

        internal ImageProcessingContext(
            ImageViewConfig config,
            DrawCanvas imageShow,
            Dispatcher dispatcher,
            ImageProcessingContextBinding binding)
        {
            Config = config;
            ImageShow = imageShow;
            Dispatcher = dispatcher;
            _binding = binding;
        }

        public ImageViewConfig Config { get; }

        public DrawCanvas ImageShow { get; }

        public Dispatcher Dispatcher { get; }

        public bool IsInitialized => _binding.IsInitialized();

        public double Width => _binding.GetWidth();

        public double Height => _binding.GetHeight();

        public HImage? HImageCache
        {
            get => _binding.GetHImageCache();
            set => _binding.SetHImageCache(value);
        }

        public ImageSource FunctionImage
        {
            get => _binding.GetFunctionImage()!;
            [param: AllowNull]
            set => _binding.SetFunctionImage(value);
        }

        public ImageSource ViewBitmapSource
        {
            get => _binding.GetViewBitmapSource()!;
            [param: AllowNull]
            set => _binding.SetViewBitmapSource(value);
        }

        public int GetSelectedLayerSourceChannelIndex()
        {
            return _binding.GetSelectedLayerSourceChannelIndex();
        }

        public void SetImageSource(ImageSource imageSource)
        {
            _binding.SetImageSource(imageSource);
        }

        public void UpdateZoomAndScale()
        {
            _binding.UpdateZoomAndScale();
        }
    }

    internal sealed class ImageProcessingContextBinding
    {
        public required Func<bool> IsInitialized { get; init; }

        public required Func<double> GetWidth { get; init; }

        public required Func<double> GetHeight { get; init; }

        public required Func<HImage?> GetHImageCache { get; init; }

        public required Action<HImage?> SetHImageCache { get; init; }

        public required Func<ImageSource?> GetFunctionImage { get; init; }

        public required Action<ImageSource?> SetFunctionImage { get; init; }

        public required Func<ImageSource?> GetViewBitmapSource { get; init; }

        public required Action<ImageSource?> SetViewBitmapSource { get; init; }

        public required Func<int> GetSelectedLayerSourceChannelIndex { get; init; }

        public required Action<ImageSource> SetImageSource { get; init; }

        public required Action UpdateZoomAndScale { get; init; }
    }
}
