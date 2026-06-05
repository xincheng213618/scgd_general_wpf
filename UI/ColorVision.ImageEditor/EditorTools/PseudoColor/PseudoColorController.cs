using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using log4net;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.EditorTools.PseudoColor
{
    internal readonly record struct PseudoColorPreviewRequest(int Version, bool IsEnabled, PseudoColorFrameRequest? Request);

    internal sealed class PseudoColorController : IPseudoColorService, IDisposable
    {
        private const string RenderTaskKey = "PseudoColorRender";

        private static readonly ILog log = LogManager.GetLogger(typeof(PseudoColorController));

        private readonly ImageProcessingContext _owner;
        private readonly PseudoColorToolState _state;
        private int _renderVersion;

        public PseudoColorController(ImageProcessingContext owner, PseudoColorToolState state)
        {
            _owner = owner;
            _state = state;
            _state.PropertyChanged += State_PropertyChanged;
        }

        public bool IsEnabled => InvokeOnUiThread(IsEnabledCore);

        public void ConfigureForImage()
        {
            _state.ApplyDefaults(PseudoColorDefaultConfig.Current);

            var depth = _owner.Config.GetProperties<int>("Depth");
            if (depth == 16)
            {
                _owner.Config.SetViewState("Max", 65535, nameof(PseudoColorController), "当前图像伪彩/阈值处理使用的像素上限");
                _state.SliderSmallChange = 255;
                _state.SliderLargeChange = 2550;
                _state.SliderMaximum = 65535;
                _state.SliderValueEnd = 65535;
            }
            else
            {
                _owner.Config.SetViewState("Max", 255, nameof(PseudoColorController), "当前图像伪彩/阈值处理使用的像素上限");
                _state.SliderSmallChange = 1;
                _state.SliderLargeChange = 10;
                _state.SliderMaximum = 255;
                _state.SliderValueEnd = 255;
            }

            TryApplyAutoRange();
        }

        public void RefreshPreview()
        {
            if (_owner.Dispatcher.CheckAccess())
            {
                _state.ColormapPreviewImage = ColormapConstats.CreatePreviewImage(_state.ColormapTypes);
            }
            else
            {
                _owner.Dispatcher.Invoke(() => _state.ColormapPreviewImage = ColormapConstats.CreatePreviewImage(_state.ColormapTypes));
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

        public void Reset()
        {
            Invalidate();
            _state.ResetForNewImage(PseudoColorDefaultConfig.Current);
            InvokeOnUiThread(() =>
            {
                RestoreSource();
                return true;
            });
        }

        public bool TryCreateRequest(out PseudoColorFrameRequest request, int? channelOverride = null)
        {
            var snapshot = InvokeOnUiThread(() =>
            {
                var channel = channelOverride ?? GetSelectedChannel();
                return (IsEnabled: IsEnabledCore(), Request: CaptureFrameRequest(channel));
            });

            request = snapshot.Request;
            return snapshot.IsEnabled;
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

        public void Dispose()
        {
            _state.PropertyChanged -= State_PropertyChanged;
        }

        public void OnColormapTypesChanged()
        {
            RefreshPreview();
            RequestRender();
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

        private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PseudoColorToolState.ColormapTypes):
                    OnColormapTypesChanged();
                    break;
                case nameof(PseudoColorToolState.IsAutoSetRange):
                    OnAutoSetRangeChanged();
                    break;
                case nameof(PseudoColorToolState.IsEnabled):
                    OnPseudoToggleChanged();
                    break;
                case nameof(PseudoColorToolState.SliderValueStart):
                case nameof(PseudoColorToolState.SliderValueEnd):
                    OnSliderValueChanged();
                    break;
            }
        }

        private T InvokeOnUiThread<T>(Func<T> action)
        {
            Dispatcher dispatcher = _owner.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                return action();
            }

            return dispatcher.Invoke(action);
        }

        private bool IsEnabledCore()
        {
            return _state.IsEnabled;
        }

        private void TryApplyAutoRange()
        {
            if (_state.IsAutoSetRange)
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

            _state.DataMin = minVal;
            _state.DataMax = maxVal;
            _state.SliderMinimum = minVal;
            _state.SliderMaximum = maxVal;
            _state.SliderValueStart = minVal;
            _state.SliderValueEnd = maxVal;
        }

        private void ResetSliderRange()
        {
            var depth = _owner.Config.GetProperties<int>("Depth");
            var defaultMax = depth == 16 ? 65535u : 255u;

            _state.DataMin = 0;
            _state.DataMax = 0;
            _state.SliderMinimum = 0;
            _state.SliderMaximum = defaultMax;
            _state.SliderValueStart = 0;
            _state.SliderValueEnd = defaultMax;
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
            var start = Math.Clamp(_state.SliderValueStart, _state.SliderMinimum, _state.SliderMaximum);
            var end = Math.Clamp(_state.SliderValueEnd, _state.SliderMinimum, _state.SliderMaximum);
            if (start > end)
            {
                (start, end) = (end, start);
            }

            return new PseudoColorFrameRequest(
                (uint)start,
                (uint)end,
                _state.ColormapTypes,
                channel,
                _state.IsAutoSetRange,
                _state.DataMin,
                _state.DataMax);
        }

        private int GetSelectedChannel()
        {
            return _owner.GetSelectedLayerSourceChannelIndex();
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
