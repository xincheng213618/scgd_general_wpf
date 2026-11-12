#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Abstractions;
using log4net;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Presentation.PseudoColor
{
    internal readonly record struct PseudoColorPreviewRequest(
        int Version,
        long ImageRevision,
        long PreviewGeneration,
        bool IsEnabled,
        PseudoColorFrameRequest? Request,
        Func<bool>? SelectionGuard = null);

    internal sealed record SelectedLayerRestorePreparation(
        ImageFrameLease? Frame,
        AlgorithmImageBuffer? CatalogInput,
        Guid DocumentId,
        ImageSource Source,
        int Channel,
        int PixelChannel,
        long ClaimSequence,
        long SelectionVersion);

    internal sealed class PseudoColorController : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PseudoColorController));
        private const int SliderRenderDelayMilliseconds = 25;

        private readonly ImageProcessingContext _owner;
        private readonly PseudoColorState _state;
        private readonly string _renderTaskKey = $"{nameof(PseudoColorController)}_{Guid.NewGuid():N}";
        private ImageAlgorithmPreviewSession? _previewSession;
        private int _renderVersion;
        private int _lastRequestedRenderVersion;
        private bool _resetting;
        private bool _deferSelectionSliderRender;
        private bool _disposed;

        public PseudoColorController(ImageProcessingContext owner, PseudoColorState state)
        {
            _owner = owner;
            _state = state;
            _state.PropertyChanged += State_PropertyChanged;
        }

        public bool IsEnabled => InvokeOnUiThread(IsEnabledCore);

        public void ConfigureForImage()
        {
            DisposePreviewSession();

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

            if (IsEnabledCore())
            {
                TryApplyAutoRange();
            }
            else
            {
                ResetSliderRange();
            }
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

        public void RequestRender(int throttleDelayMs = 0, Func<bool>? selectionGuard = null)
        {
            if (_owner.StreamPresentation.IsActive)
            {
                RetirePreviewForStreamSelection();
                _owner.StreamPresentation.RefreshCurrent();
                return;
            }
            var request = CapturePreviewRequest() with { SelectionGuard = selectionGuard };
            TaskConflator.RunOrUpdate(_renderTaskKey, async () =>
            {
                await RenderAsync(request);
            }, throttleDelayMs);
        }

        internal void RetirePreviewForStreamSelection()
        {
            Invalidate();
            DisposePreviewSession();
        }

        internal bool TryRefreshOwnedStaticSelection(int channel)
        {
            _owner.Dispatcher.VerifyAccess();
            ImageAlgorithmPreviewSession? session = _previewSession;
            ImageSource? source = _owner.ViewBitmapSource;
            if (!IsEnabledCore() || _owner.StreamPresentation.IsActive || channel is < -1 or > 2
                || channel != GetSelectedChannel() || source is not BitmapSource bitmap
                || (bitmap.Format != PixelFormats.Bgr24 && bitmap.Format != PixelFormats.Bgr32
                    && bitmap.Format != PixelFormats.Bgra32 && bitmap.Format != PixelFormats.Pbgra32
                    && bitmap.Format != PixelFormats.Rgb24 && bitmap.Format != PixelFormats.Rgb48
                    && bitmap.Format != PixelFormats.Rgba64)
                || session == null || session.SourceRevision != _owner.ImageRevision
                || !session.UsesSource(source) || !session.OwnsHostPreview) return false;

            Guid documentId = _owner.DocumentInstanceId;
            long revision = _owner.ImageRevision;
            long previewGeneration = _owner.AlgorithmPreviewGeneration;
            Invalidate();
            int version = Volatile.Read(ref _renderVersion);
            ImagePresentationRequest presentation = _owner.Presentation.BeginRequest();

            bool IsCurrentSelection() => IsEnabledCore() && !_owner.StreamPresentation.IsActive
                && _owner.DocumentInstanceId == documentId && _owner.IsCurrentImageRevision(revision)
                && ReferenceEquals(_owner.ViewBitmapSource, source) && ReferenceEquals(_previewSession, session)
                && session.OwnsHostPreview && _owner.AlgorithmPreviewGeneration == previewGeneration
                && GetSelectedChannel() == channel && _owner.Presentation.IsCurrent(presentation);
            bool CanUpdateRange() => IsCurrentSelection() && version == Volatile.Read(ref _renderVersion);

            if (_state.IsAutoSetRange)
            {
                bool wasDeferring = _deferSelectionSliderRender;
                _deferSelectionSliderRender = true;
                try { ApplyAutoRange(CanUpdateRange); }
                finally { _deferSelectionSliderRender = wasDeferring; }
            }
            if (CanUpdateRange()) RequestRender(selectionGuard: IsCurrentSelection);
            // Once this owned route was selected, callbacks may have installed a successor.
            // Never fall back to ordinary channel rendering after losing that ownership.
            return true;
        }

        public void Invalidate()
        {
            Interlocked.Increment(ref _renderVersion);
        }

        public void Reset()
        {
            Invalidate();
            bool wasResetting = _resetting;
            _resetting = true;
            try
            {
                _state.IsEnabled = false;
                _state.ResetImageRange();
                InvokeOnUiThread(() =>
                {
                    RestoreSource();
                    return true;
                });
            }
            finally { _resetting = wasResetting; }
        }

        private bool TryCreateFrameRequest(out PseudoColorFrameRequest request)
        {
            var snapshot = InvokeOnUiThread(() =>
            {
                var channel = GetSelectedChannel();
                if (_owner.ViewBitmapSource is BitmapSource source) channel = ToCatalogChannel(source, channel);
                return (IsEnabled: IsEnabledCore(), Request: CaptureFrameRequest(channel));
            });

            request = snapshot.Request;
            return snapshot.IsEnabled;
        }

        // Catalog input is canonical BGR; frame/native requests retain source ordering.
        private static int ToCatalogChannel(BitmapSource source, int channel)
            => (channel is 0 or 2) && (source.Format == PixelFormats.Rgb24 || source.Format == PixelFormats.Rgb48
                || source.Format == PixelFormats.Rgba64) ? 2 - channel : channel;

        public bool TryCaptureFrameRequest(out PseudoColorFrameRequest request)
        {
            var snapshot = InvokeOnUiThread(() =>
            {
                int channel = GetSelectedChannel();
                return (
                    IsEnabled: IsEnabledCore(),
                    HasSource: _owner.ViewBitmapSource != null,
                    FrameRequest: CaptureFrameRequest(channel));
            });

            request = snapshot.FrameRequest;
            return snapshot.IsEnabled && snapshot.HasSource;
        }

        public void RestoreSource()
        {
            bool restoredOwnedPreview = DisposePreviewSession();
            if (restoredOwnedPreview || _owner.HasActiveAlgorithmPreview) return;
            _owner.Presentation.RestoreSource();
        }

        private void RestoreSelectedLayer(PseudoColorPreviewRequest request, WriteableBitmap? preparedChannel = null)
        {
            Guid documentId = _owner.DocumentInstanceId;
            long revision = _owner.ImageRevision;
            ImageSource? source = _owner.ViewBitmapSource;
            long claimSequence = _owner.AlgorithmClaimSequence;
            long selectionVersion = _owner.Presentation.SelectionVersion;
            bool restoredOwnedPreview = DisposePreviewSession();
            // An owned Cancel publishes the canonical source once. Any additional display
            // selection or claim belongs to a synchronous callback and must remain intact.
            long expectedSelectionVersion = selectionVersion + (restoredOwnedPreview ? 1 : 0);
            if (_disposed || _owner.IsDisposed || _owner.DocumentInstanceId != documentId
                || !_owner.IsCurrentImageRevision(revision) || !ReferenceEquals(_owner.ViewBitmapSource, source)
                || _owner.AlgorithmClaimSequence != claimSequence
                || _owner.HasActiveAlgorithmPreview || _owner.Presentation.SelectionVersion != expectedSelectionVersion) return;
            int currentVersion = Volatile.Read(ref _renderVersion);
            if (request.Version != currentVersion)
            {
                // A synchronous toggle can capture before Cancel advances the preview generation.
                // Rebase an actual queued request; invalidation alone must not revive rendering.
                if (restoredOwnedPreview && currentVersion == Volatile.Read(ref _lastRequestedRenderVersion)) RequestRender();
                return;
            }
            if (preparedChannel != null) _owner.Presentation.Publish(preparedChannel, preparedChannel);
            else _owner.RestoreSelectedLayerDisplay();
        }

        private SelectedLayerRestorePreparation? CaptureSelectedLayerRestore(PseudoColorPreviewRequest request)
        {
            _owner.Dispatcher.VerifyAccess();
            if (!IsCurrentRequest(request)) return null;
            int channel = GetSelectedChannel();
            ImageSource? source = _owner.ViewBitmapSource;
            if (channel < 0 || source == null || _owner.StreamPresentation.IsActive)
            {
                RestoreSelectedLayer(request);
                return null;
            }

            Guid documentId = _owner.DocumentInstanceId;
            long claimSequence = _owner.AlgorithmClaimSequence;
            long selectionVersion = _owner.Presentation.SelectionVersion;
            bool IsCaptureCurrent() => IsCurrentRequest(request) && _owner.DocumentInstanceId == documentId
                && ReferenceEquals(_owner.ViewBitmapSource, source) && GetSelectedChannel() == channel
                && _owner.AlgorithmClaimSequence == claimSequence && _owner.Presentation.SelectionVersion == selectionVersion;

            // Only the default bitmap layer contract identifies these raw RGB samples with
            // the catalog snapshot. Pbgra32 is unpremultiplied there and must use raw pixels.
            if (_owner.CanRestoreChannelFromCatalogSource && channel < 3 && source is BitmapSource bitmap
                && (bitmap.Format == PixelFormats.Bgr24 || bitmap.Format == PixelFormats.Bgr32
                    || bitmap.Format == PixelFormats.Bgra32 || bitmap.Format == PixelFormats.Rgb24 || bitmap.Format == PixelFormats.Rgb48))
            {
                AlgorithmImageBuffer? input = _previewSession?.TryRetainSourceForDisplay(source, request.ImageRevision);
                try
                {
                    if (!IsCaptureCurrent()) return null;
                    if (input != null && input.Width == bitmap.PixelWidth && input.Height == bitmap.PixelHeight
                        && input.Format == ImageAlgorithmInputFactory.FromPixelFormat(bitmap.Format))
                    {
                        SelectedLayerRestorePreparation preparation = new(null, input, documentId, source, channel,
                            ToCatalogChannel(bitmap, channel), claimSequence, selectionVersion);
                        input = null;
                        return preparation;
                    }
                }
                finally { input?.Dispose(); }
            }

            if (!IsCaptureCurrent()) return null;
            ImageFrameLease? frame;
            try { frame = _owner.AcquireImageFrame(); }
            catch (Exception ex)
            {
                log.Warn("Unable to acquire the selected channel before disabling pseudo color.", ex);
                frame = null;
            }
            if (!IsCaptureCurrent())
            {
                frame?.Dispose();
                return null;
            }
            if (frame == null || frame.Image.channels < 3 || channel >= frame.Image.channels || frame.Image.depth is not (8 or 16))
            {
                frame?.Dispose();
                RestoreSelectedLayer(request);
                return null;
            }
            return new(frame, null, documentId, source, channel, channel, claimSequence, selectionVersion);
        }

        private static WriteableBitmap PrepareSelectedLayerChannel(SelectedLayerRestorePreparation preparation)
        {
            if (preparation.CatalogInput is not { } input)
                return ImageChannelPresenter.PrepareChannel(preparation.Frame!.Image, preparation.PixelChannel);

            using AlgorithmImageMatLease borrowed = AlgorithmImageInterop.BorrowReadOnly(input);
            HImage source = new()
            {
                rows = input.Height, cols = input.Width, stride = input.Stride,
                channels = input.Format.Channels(), depth = input.Format.BitsPerChannel(),
                pData = borrowed.Mat.Data, isDispose = true,
            };
            return ImageChannelPresenter.PrepareChannel(source, preparation.PixelChannel);
        }

        private async Task RestoreSelectedLayerAsync(PseudoColorPreviewRequest request)
        {
            SelectedLayerRestorePreparation? preparation = await _owner.Dispatcher.InvokeAsync(() => CaptureSelectedLayerRestore(request));
            if (preparation == null) return;
            WriteableBitmap? prepared = null;
            using (preparation.Frame)
            using (preparation.CatalogInput)
            {
                try { prepared = await Task.Run(() => PrepareSelectedLayerChannel(preparation)); }
                catch (Exception ex) { log.Warn("Unable to prepare the selected channel before disabling pseudo color.", ex); }
            }
            await _owner.Dispatcher.InvokeAsync(() =>
            {
                if (!IsCurrentRequest(request) || _owner.DocumentInstanceId != preparation.DocumentId
                    || !ReferenceEquals(_owner.ViewBitmapSource, preparation.Source) || GetSelectedChannel() != preparation.Channel
                    || _owner.AlgorithmClaimSequence != preparation.ClaimSequence
                    || _owner.Presentation.SelectionVersion != preparation.SelectionVersion) return;
                // Preserve Cancel's synchronous callbacks, then restore the prepared channel
                // without yielding to the dispatcher between the two display publications.
                RestoreSelectedLayer(request, prepared);
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Invalidate();
            DisposePreviewSession();
            _state.PropertyChanged -= State_PropertyChanged;
        }

        public void OnColormapTypesChanged()
        {
            RefreshPreview();
            if (IsEnabledCore()) RequestRender();
        }

        public void OnAutoSetRangeChanged()
        {
            if (!_owner.IsInitialized || !IsEnabledCore())
            {
                return;
            }

            TryApplyAutoRange();
            if (_deferSelectionSliderRender) RequestRender();
        }

        public void OnPseudoToggleChanged()
        {
            if (IsEnabledCore())
            {
                TryApplyAutoRange();
            }
            RequestRender();
        }

        public void OnSelectedLayerChanged()
        {
            if (_disposed || _owner.IsDisposed) return;
            bool enabled = IsEnabledCore();
            if (!enabled && !_owner.StreamPresentation.IsActive) return;
            if (enabled && _state.IsAutoSetRange) ApplyAutoRange();
            RequestRender();
        }

        public void OnSliderValueChanged()
        {
            if (_deferSelectionSliderRender || !_owner.IsInitialized || !IsEnabledCore())
            {
                return;
            }

            // TaskConflator still runs one preview at a time and keeps only the latest pending request.
            RequestRender(SliderRenderDelayMilliseconds);
        }

        private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_resetting) return;
            switch (e.PropertyName)
            {
                case nameof(PseudoColorState.ColormapTypes):
                    OnColormapTypesChanged();
                    break;
                case nameof(PseudoColorState.IsAutoSetRange):
                    OnAutoSetRangeChanged();
                    break;
                case nameof(PseudoColorState.IsEnabled):
                    OnPseudoToggleChanged();
                    break;
                case nameof(PseudoColorState.SliderValueStart):
                case nameof(PseudoColorState.SliderValueEnd):
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
            return !_disposed && !_owner.IsDisposed && _state.IsEnabled;
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

        private void ApplyAutoRange(Func<bool>? canContinue = null)
        {
            if (canContinue?.Invoke() == false) return;
            using ImageFrameLease? lease = _owner.AcquireImageFrame();
            if (lease == null || canContinue?.Invoke() == false)
            {
                return;
            }

            var channel = GetSelectedChannel();
            HImage pixels = lease.Image;
            using var luminance = _owner.ViewBitmapSource is BitmapSource bitmap
                && ImageStreamPresentation.NeedsRgbLuminance(bitmap.Format, pixels, channel)
                ? ImageStreamPresentation.CreateRgbLuminance(pixels, out pixels) : null;
            var ret = OpenCVMediaHelper.M_GetMinMax(pixels, out uint minVal, out uint maxVal, channel);
            if (ret != 0)
            {
                return;
            }

            if (minVal >= maxVal)
            {
                var depth = _owner.Config.GetProperties<int>("Depth");
                maxVal = depth == 16 ? Math.Min(minVal + 1, (uint)65535) : Math.Min(minVal + 1, 255u);
            }

            if (canContinue?.Invoke() == false) return;
            _state.DataMin = minVal;
            if (canContinue?.Invoke() == false) return;
            _state.DataMax = maxVal;
            if (canContinue?.Invoke() == false) return;
            _state.SliderMinimum = minVal;
            if (canContinue?.Invoke() == false) return;
            _state.SliderMaximum = maxVal;
            if (canContinue?.Invoke() == false) return;
            _state.SliderValueStart = minVal;
            if (canContinue?.Invoke() == false) return;
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
            Volatile.Write(ref _lastRequestedRenderVersion, version);
            var imageRevision = _owner.ImageRevision;
            var previewGeneration = _owner.AlgorithmPreviewGeneration;
            if (TryCreateFrameRequest(out var request))
            {
                return new PseudoColorPreviewRequest(version, imageRevision, previewGeneration, true, request);
            }

            return new PseudoColorPreviewRequest(version, imageRevision, previewGeneration, false, null);
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
            return request.Version == Volatile.Read(ref _renderVersion)
                && _owner.IsCurrentImageRevision(request.ImageRevision)
                && request.PreviewGeneration == _owner.AlgorithmPreviewGeneration
                && request.SelectionGuard?.Invoke() != false;
        }

        private async Task RenderAsync(PseudoColorPreviewRequest request)
        {
            if (!request.IsEnabled || request.Request == null)
            {
                await RestoreSelectedLayerAsync(request);
                return;
            }

            var frameRequest = request.Request.Value;
            long previewGeneration = 0;
            ImageAlgorithmPreviewSession? session = await _owner.Dispatcher.InvokeAsync(() =>
            {
                if (!IsCurrentRequest(request) || !IsEnabled) return null;
                if (_previewSession == null
                    || _previewSession.SourceRevision != request.ImageRevision
                    || !_previewSession.OwnsHostPreview)
                {
                    DisposePreviewSession();
                    _previewSession = ImageAlgorithmPreviewSession.StartForCatalogPreview(_owner);
                }
                previewGeneration = _owner.AlgorithmPreviewGeneration;
                return _previewSession;
            });
            if (session == null) return;

            // Initial Start advances the owner generation itself. Bind after it succeeds,
            // while the request version still prevents older in-flight results publishing.
            bool CanContinue() => IsEnabledCore() && request.Version == Volatile.Read(ref _renderVersion)
                && _owner.IsCurrentImageRevision(request.ImageRevision)
                && ReferenceEquals(_previewSession, session) && session.UsesSource(_owner.ViewBitmapSource)
                && session.OwnsHostPreview && _owner.AlgorithmPreviewGeneration == previewGeneration
                && request.SelectionGuard?.Invoke() != false;

            PseudoColorParameters parameters = new()
            {
                UseNominalRange = false,
                Minimum = frameRequest.Min,
                Maximum = frameRequest.Max,
                Colormap = (StandardPseudoColorMap)(int)frameRequest.ColormapTypes,
                Channel = frameRequest.Channel,
                AutoRange = frameRequest.HasValidAutoRange,
                DataMinimum = frameRequest.DataMin,
                DataMaximum = frameRequest.DataMax,
            };
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.PseudoColor, parameters);
            using AlgorithmResult result = await session.PreviewAsync(invocation, canContinue: CanContinue);
            if (result.Status == AlgorithmResultStatus.Failed)
                log.Warn($"PseudoColor failed: {string.Join("; ", result.Failures)}");
        }

        private bool DisposePreviewSession()
        {
            ImageAlgorithmPreviewSession? session = _previewSession;
            _previewSession = null;
            if (session == null) return false;
            try { return session.Cancel(); }
            finally { session.Dispose(); }
        }
    }
}
