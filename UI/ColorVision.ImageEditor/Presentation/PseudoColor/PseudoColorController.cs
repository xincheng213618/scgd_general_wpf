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
        PseudoColorFrameRequest? Request);

    internal sealed record SelectedLayerRestorePreparation(
        ImageFrameLease Frame,
        Guid DocumentId,
        ImageSource Source,
        int Channel,
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

        public void RequestRender(int throttleDelayMs = 0)
        {
            if (_owner.StreamPresentation.IsActive)
            {
                Invalidate();
                DisposePreviewSession();
                _owner.StreamPresentation.RefreshCurrent();
                return;
            }
            var request = CapturePreviewRequest();
            TaskConflator.RunOrUpdate(_renderTaskKey, async () =>
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
                // Catalog input is canonical BGR; frame/native requests retain source ordering.
                if ((channel is 0 or 2) && _owner.ViewBitmapSource is BitmapSource source
                    && (source.Format == PixelFormats.Rgb24 || source.Format == PixelFormats.Rgb48 || source.Format == PixelFormats.Rgba64))
                    channel = 2 - channel;
                return (IsEnabled: IsEnabledCore(), Request: CaptureFrameRequest(channel));
            });

            request = snapshot.Request;
            return snapshot.IsEnabled;
        }

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
            ImageFrameLease? frame;
            try { frame = _owner.AcquireImageFrame(); }
            catch (Exception ex)
            {
                log.Warn("Unable to acquire the selected channel before disabling pseudo color.", ex);
                frame = null;
            }
            if (!IsCurrentRequest(request) || _owner.DocumentInstanceId != documentId
                || !ReferenceEquals(_owner.ViewBitmapSource, source) || GetSelectedChannel() != channel
                || _owner.AlgorithmClaimSequence != claimSequence || _owner.Presentation.SelectionVersion != selectionVersion)
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
            return new(frame, documentId, source, channel, claimSequence, selectionVersion);
        }

        private async Task RestoreSelectedLayerAsync(PseudoColorPreviewRequest request)
        {
            SelectedLayerRestorePreparation? preparation = await _owner.Dispatcher.InvokeAsync(() => CaptureSelectedLayerRestore(request));
            if (preparation == null) return;
            using ImageFrameLease frame = preparation.Frame;
            WriteableBitmap? prepared = null;
            try
            {
                prepared = await Task.Run(() => ImageChannelPresenter.PrepareChannel(frame.Image, preparation.Channel));
            }
            catch (Exception ex)
            {
                log.Warn("Unable to prepare the selected channel before disabling pseudo color.", ex);
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
            if (!_owner.IsInitialized || !IsEnabledCore())
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

        private void ApplyAutoRange()
        {
            using ImageFrameLease? lease = _owner.AcquireImageFrame();
            if (lease == null)
            {
                return;
            }

            var channel = GetSelectedChannel();
            var ret = OpenCVMediaHelper.M_GetMinMax(lease.Image, out uint minVal, out uint maxVal, channel);
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
                && request.PreviewGeneration == _owner.AlgorithmPreviewGeneration;
        }

        private async Task RenderAsync(PseudoColorPreviewRequest request)
        {
            if (!request.IsEnabled || request.Request == null)
            {
                await RestoreSelectedLayerAsync(request);
                return;
            }

            var frameRequest = request.Request.Value;
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
                return _previewSession;
            });
            if (session == null) return;

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
            using AlgorithmResult result = await session.PreviewAsync(invocation);
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
