using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.ImageEditor;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Templates.POI;

internal sealed class PoiBackgroundCapture
{
    private static readonly ILog log = LogManager.GetLogger(typeof(PoiBackgroundCapture));
    private readonly Func<DeviceCamera?> selectCamera;
    private readonly Func<DeviceCamera, double, MsgRecord?> takePhoto;
    private CancellationTokenSource? pending;

    internal PoiBackgroundCapture(Func<DeviceCamera?>? selectCamera = null, Func<DeviceCamera, double, MsgRecord?>? takePhoto = null)
    {
        this.selectCamera = selectCamera ?? (() => ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault());
        this.takePhoto = takePhoto ?? ((camera, exposure) => camera.DisplayCameraControlLazy.Value.TakePhoto(exposure));
    }

    internal void Cancel()
    {
        var request = pending;
        pending = null;
        request?.Cancel();
    }

    internal async Task CaptureAsync(ImageView imageView, Func<PoiConfig> getConfig, double exposure = 0)
    {
        Cancel();
        if (selectCamera() is not { } camera) return;

        var source = new Source(camera);
        if (!source.IsCurrent) return;

        PoiConfig config = getConfig();
        string? background = config.BackgroundFilePath;
        long revision = imageView.ImageRevision;
        using var request = new CancellationTokenSource();
        pending = request;
        config.PropertyChanged += BackgroundChanged;
        Task? opening = null;
        string? openedPath = null;
        long? openedRevision = null;
        try
        {
            if (takePhoto(camera, exposure) is not { } record) return;
            var result = await source.ReadResultAsync(record, request.Token);
            if (result == null || !OwnsRequest() || !imageView.IsCurrentImageRevision(revision)) return;

            if (string.IsNullOrWhiteSpace(result.FileUrl))
            {
                MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailedNoPath);
                return;
            }

            openedPath = result.FileUrl;
            if (!imageView.TryOpenImage(openedPath))
            {
                MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailed, result.FileUrl);
                return;
            }

            opening = imageView.ImageOpenCompletion;
            if (opening.IsCompleted) openedRevision = imageView.ImageRevision;
            else imageView.ImageSourceLoaded += ImageLoaded;
            using var retireOpening = request.Token.Register(() =>
            {
                if (imageView.Dispatcher.CheckAccess()) CancelOpening();
                else imageView.Dispatcher.BeginInvoke(CancelOpening);
            });
            await opening.WaitAsync(request.Token);
            if (OwnsRequest() && OwnsOpening() && openedRevision is long currentRevision
                && imageView.IsCurrentImageRevision(currentRevision) && imageView.ViewBitmapSource != null)
            {
                config.PropertyChanged -= BackgroundChanged;
                config.BackgroundFilePath = openedPath;
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error)
        {
            log.Warn("Unable to use the camera capture as the POI background.", error);
            if (OwnsRequest() && imageView.IsCurrentImageRevision(revision) && (opening == null || OwnsOpening()))
                MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailed, error.Message);
        }
        finally
        {
            config.PropertyChanged -= BackgroundChanged;
            imageView.ImageSourceLoaded -= ImageLoaded;
            if (ReferenceEquals(pending, request)) pending = null;
        }

        bool OwnsRequest() => ReferenceEquals(pending, request) && !request.IsCancellationRequested
            && source.IsCurrent && ReferenceEquals(selectCamera(), camera) && ReferenceEquals(getConfig(), config)
            && string.Equals(config.BackgroundFilePath, background, StringComparison.Ordinal);

        void BackgroundChanged(object? sender, PropertyChangedEventArgs change)
        {
            if (change.PropertyName == nameof(PoiConfig.BackgroundFilePath)
                && !string.Equals(config.BackgroundFilePath, background, StringComparison.Ordinal)) request.Cancel();
        }

        bool OwnsOpening() => ReferenceEquals(imageView.ImageOpenCompletion, opening)
            && string.Equals(imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), openedPath, StringComparison.Ordinal);

        void ImageLoaded(object? sender, ImageViewImageSourceLoadedEventArgs e)
        {
            if (opening is { IsCompleted: false } && OwnsOpening()) openedRevision = e.ImageRevision;
        }

        void CancelOpening()
        {
            try
            {
                if (opening is { IsCompleted: false } && OwnsOpening()) imageView.ReleaseImageContent();
            }
            catch (Exception error) { log.Warn("Unable to cancel the POI background image open.", error); }
        }
    }

    internal sealed class Source
    {
        private readonly DeviceCamera camera;
        private readonly MQTTCamera service;
        private readonly string deviceCode;
        private readonly bool local;
        private readonly MqttServiceRoute? route;
        private readonly MySqlConnectedEndpoint? endpoint;

        internal Source(DeviceCamera camera)
        {
            this.camera = camera;
            service = camera.DService;
            deviceCode = camera.Config.Code;
            local = camera.RoutesLocally;
            route = local ? null : service.CaptureMqttRoute(pinConnection: true);
            var candidate = camera.ResultDatabaseEndpoint;
            endpoint = camera.OwnsResultDatabase(candidate) ? candidate : null;
        }

        internal bool IsCurrent => !camera.IsDisposed && ReferenceEquals(camera.DService, service)
            && string.Equals(camera.Config.Code, deviceCode, StringComparison.Ordinal) && camera.RoutesLocally == local
            && (endpoint == null || camera.OwnsResultDatabase(endpoint))
            && (route == null || (service.CanPresentFeedback(camera, serviceOnly: true)
                && route.HasSameDestination(service.CaptureMqttRoute(pinConnection: true))));

        internal async Task<MeasureResultImgModel?> ReadResultAsync(MsgRecord record, CancellationToken cancellationToken = default)
        {
            if (endpoint == null || !IsCurrent || record.MsgSend?.EventName != "GetData" || string.IsNullOrEmpty(record.MsgID)
                || (!string.IsNullOrEmpty(record.MsgSend.DeviceCode) && !string.Equals(deviceCode, record.MsgSend.DeviceCode, StringComparison.Ordinal))
                || (route != null && (!string.Equals(route.SendTopic, record.SendTopic, StringComparison.Ordinal)
                    || !string.Equals(route.ReplyTopic, record.SubscribeTopic, StringComparison.Ordinal)))) return null;
            if (await WaitForCompletionAsync(record, cancellationToken).ConfigureAwait(false) != MsgRecordState.Success
                || !IsCurrent || record.MsgReturn is not { HasCode: true, Code: 0 } reply
                || !string.Equals(record.MsgID, reply.MsgID, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(reply.DeviceCode) && !string.Equals(deviceCode, reply.DeviceCode, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(reply.EventName) && reply.EventName != "GetData")) return null;

            object? payload = reply.Data;
            var data = payload as JObject;
            if (data == null && local && payload != null) data = JObject.FromObject(payload);
            if (!MsgReturn.TryReadInteger(data?["MasterId"], out int masterId) || masterId <= 0) return null;

            var result = await Task.Run(() => IsCurrent ? MeasureImgResultDao.ReadResult(endpoint, masterId) : null,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result != null && IsCurrent && (string.IsNullOrWhiteSpace(result.DeviceCode)
                || string.Equals(deviceCode, result.DeviceCode, StringComparison.Ordinal)) ? result : null;
        }

        private static async Task<MsgRecordState> WaitForCompletionAsync(MsgRecord record, CancellationToken cancellationToken)
        {
            if (record.WorkerCompletion is { } worker) return await worker.WaitAsync(cancellationToken).ConfigureAwait(false);

            var completed = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
            record.TerminalStateCommitted += OnTerminal;
            try
            {
                OnTerminal(record.MsgRecordState);
                return await completed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally { record.TerminalStateCommitted -= OnTerminal; }

            void OnTerminal(MsgRecordState state)
            {
                if (MsgRecord.IsTerminal(state)) completed.TrySetResult(state);
            }
        }
    }
}
