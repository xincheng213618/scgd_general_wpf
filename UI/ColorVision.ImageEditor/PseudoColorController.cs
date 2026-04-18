using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using log4net;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    internal readonly record struct PseudoColorPreviewRequest(int Version, bool IsEnabled, PseudoColorFrameRequest? Request);

    internal sealed class PseudoColorController : IPseudoColorService
    {
        private const string RenderTaskKey = "PseudoSlider";

        private static readonly ILog log = LogManager.GetLogger(typeof(PseudoColorController));

        private readonly ImageView _owner;
        private int _renderVersion;

        public PseudoColorController(ImageView owner)
        {
            _owner = owner;
        }

        public bool IsEnabled => _owner.Pseudo.IsChecked == true;

        public void ConfigureForImage()
        {
            var depth = _owner.Config.GetProperties<int>("Depth");
            if (depth == 16)
            {
                _owner.Config.AddProperties("Max", 65535);
                _owner.PseudoSlider.SmallChange = 255;
                _owner.PseudoSlider.LargeChange = 2550;
                _owner.PseudoSlider.Maximum = 65535;
                _owner.PseudoSlider.ValueEnd = 65535;
            }
            else
            {
                _owner.Config.AddProperties("Max", 255);
                _owner.PseudoSlider.SmallChange = 1;
                _owner.PseudoSlider.LargeChange = 10;
                _owner.PseudoSlider.Maximum = 255;
                _owner.PseudoSlider.ValueEnd = 255;
            }

            TryApplyAutoRange();
        }

        public void RefreshPreview()
        {
            if (_owner.ColormapTypesImage.Dispatcher.CheckAccess())
            {
                _owner.ColormapTypesImage.Source = ColormapConstats.CreatePreviewImage(_owner.Config.ColormapTypes);
            }
            else
            {
                _owner.ColormapTypesImage.Source = _owner.ColormapTypesImage.Dispatcher.Invoke(() => ColormapConstats.CreatePreviewImage(_owner.Config.ColormapTypes));
            }
        }

        public void RequestRender(int throttleDelayMs = 0)
        {
            var request = CapturePreviewRequest();
            TaskConflator.RunOrUpdate(RenderTaskKey, async () =>
            {
                await RenderAsync(request);
            }, throttleDelayMs);
        }

        public void Invalidate()
        {
            Interlocked.Increment(ref _renderVersion);
        }

        public bool TryCreateRequest(out PseudoColorFrameRequest request, int? channelOverride = null)
        {
            request = CaptureFrameRequest(channelOverride ?? GetSelectedChannel());
            return IsEnabled;
        }

        public void ApplyProcessedImage(HImage pseudoImage)
        {
            if (!IsEnabled)
            {
                pseudoImage.Dispose();
                return;
            }

            if (!HImageExtension.UpdateWriteableBitmap(_owner.FunctionImage, pseudoImage))
            {
                var image = pseudoImage.ToWriteableBitmap();
                pseudoImage.Dispose();
                _owner.FunctionImage = image;
            }

            if (IsEnabled)
            {
                _owner.ImageShow.Source = _owner.FunctionImage;
            }
        }

        public void RestoreSource()
        {
            _owner.ImageShow.Source = _owner.ViewBitmapSource;
            _owner.FunctionImage = null;
        }

        public void OnColormapTypesChanged()
        {
            RefreshPreview();
            RequestRender();
        }

        public void OnAutoSetRangeRequested()
        {
            TryApplyAutoRange();
        }

        public void OnAutoSetRangeChanged()
        {
            if (!_owner.IsInitialized)
            {
                return;
            }

            TryApplyAutoRange();
        }

        public void OnPseudoToggleChanged()
        {
            RequestRender();
        }

        public void OnSliderValueChanged()
        {
            if (!_owner.IsInitialized)
            {
                return;
            }

            RequestRender(100);
        }

        private void TryApplyAutoRange()
        {
            if (_owner.Config.IsAutoSetRange)
            {
                ApplyAutoRange();
            }
            else
            {
                ResetSliderRange();
            }
        }

        private void ApplyAutoRange()
        {
            var sourceImage = _owner.HImageCache;
            if (sourceImage == null)
            {
                return;
            }

            var channel = GetSelectedChannel();
            var ret = OpenCVMediaHelper.M_GetMinMax((HImage)sourceImage, out uint minVal, out uint maxVal, channel);
            if (ret != 0)
            {
                return;
            }

            if (minVal >= maxVal)
            {
                var depth = _owner.Config.GetProperties<int>("Depth");
                maxVal = depth == 16 ? Math.Min(minVal + 1, (uint)65535) : Math.Min(minVal + 1, 255u);
            }

            _owner.Config.DataMin = minVal;
            _owner.Config.DataMax = maxVal;
            _owner.PseudoSlider.Minimum = minVal;
            _owner.PseudoSlider.Maximum = maxVal;
            _owner.PseudoSlider.ValueStart = minVal;
            _owner.PseudoSlider.ValueEnd = maxVal;
        }

        private void ResetSliderRange()
        {
            var depth = _owner.Config.GetProperties<int>("Depth");
            var defaultMax = depth == 16 ? 65535u : 255u;

            _owner.Config.DataMin = 0;
            _owner.Config.DataMax = 0;
            _owner.PseudoSlider.Minimum = 0;
            _owner.PseudoSlider.Maximum = defaultMax;
            _owner.PseudoSlider.ValueStart = 0;
            _owner.PseudoSlider.ValueEnd = defaultMax;
        }

        private PseudoColorPreviewRequest CapturePreviewRequest()
        {
            var version = Interlocked.Increment(ref _renderVersion);
            if (TryCreateRequest(out var request))
            {
                return new PseudoColorPreviewRequest(version, true, request);
            }

            return new PseudoColorPreviewRequest(version, false, null);
        }

        private PseudoColorFrameRequest CaptureFrameRequest(int channel)
        {
            return new PseudoColorFrameRequest(
                (uint)_owner.PseudoSlider.ValueStart,
                (uint)_owner.PseudoSlider.ValueEnd,
                _owner.Config.ColormapTypes,
                channel,
                _owner.Config.IsAutoSetRange,
                _owner.Config.DataMin,
                _owner.Config.DataMax);
        }

        private int GetSelectedChannel()
        {
            return _owner.ComboBoxLayers.SelectedIndex - 1;
        }

        private bool IsCurrentRequest(PseudoColorPreviewRequest request)
        {
            return request.Version == Volatile.Read(ref _renderVersion);
        }

        private async Task RenderAsync(PseudoColorPreviewRequest request)
        {
            if (!request.IsEnabled || request.Request == null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!IsCurrentRequest(request))
                    {
                        return;
                    }

                    RestoreSource();
                });
                return;
            }

            if (!IsCurrentRequest(request))
            {
                return;
            }

            var sourceImage = _owner.HImageCache;
            if (sourceImage == null)
            {
                return;
            }

            var frameRequest = request.Request.Value;
            var stopwatch = Stopwatch.StartNew();
            HImage hImageProcessed = new();
            int ret;

            if (frameRequest.HasValidAutoRange)
            {
                ret = OpenCVMediaHelper.M_PseudoColorAutoRange((HImage)sourceImage, out hImageProcessed, frameRequest.Min, frameRequest.Max, frameRequest.ColormapTypes, frameRequest.Channel, frameRequest.DataMin, frameRequest.DataMax);
            }
            else
            {
                ret = OpenCVMediaHelper.M_PseudoColor((HImage)sourceImage, out hImageProcessed, frameRequest.Min, frameRequest.Max, frameRequest.ColormapTypes, frameRequest.Channel);
            }

            var algoMs = stopwatch.Elapsed.TotalMilliseconds;

            if (ret != 0)
            {
                hImageProcessed.Dispose();
                return;
            }

            if (!IsCurrentRequest(request))
            {
                hImageProcessed.Dispose();
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (!IsCurrentRequest(request) || !IsEnabled)
                {
                    hImageProcessed.Dispose();
                    return;
                }

                var updateSuccess = false;
                if (_owner.FunctionImage is WriteableBitmap validBitmap)
                {
                    updateSuccess = await HImageExtension.UpdateWriteableBitmapAsync(validBitmap, hImageProcessed);
                }

                if (!updateSuccess)
                {
                    var newBitmap = await hImageProcessed.ToWriteableBitmapAsync();
                    _owner.FunctionImage = newBitmap;
                    hImageProcessed.Dispose();
                }

                if (_owner.ImageShow.Source != _owner.FunctionImage)
                {
                    _owner.ImageShow.Source = _owner.FunctionImage;
                }

                stopwatch.Stop();
                if (log.IsInfoEnabled)
                {
                    var totalMs = stopwatch.Elapsed.TotalMilliseconds;
                    log.Info($"Algo: {algoMs:F2}ms | Render: {totalMs - algoMs:F2}ms | Total: {totalMs:F2}ms | Range: {frameRequest.Min}-{frameRequest.Max}");
                }
            });
        }
    }
}