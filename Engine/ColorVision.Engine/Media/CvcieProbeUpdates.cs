using ColorVision.Engine.Services.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using log4net;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Media;

/// <summary>Measures one sample at a time and keeps the most recent waiting sample.</summary>
internal sealed class CvcieProbeUpdates(ImageView view,
    Func<PoiMeasurementPoint, CancellationToken, ValueTask<CvcieProbeResult?>> measure,
    Action<ImagePixelSample, PoiMeasurementPoint, CvcieProbeResult> render)
{
    private static readonly ILog log = LogManager.GetLogger(typeof(CvcieProbeUpdates));
    private (ImagePixelSample Sample, PoiMeasurementPoint Point, long ContentVersion)? _latest;
    private CancellationTokenSource? _running;

    public async void Update(ImagePixelSample sample, PoiMeasurementPoint point)
    {
        _latest = (sample, point, view.ContentVersion);
        if (_running != null) return;

        while (_latest is { } next)
        {
            _latest = null;
            if (next.ContentVersion != view.ContentVersion) continue;
            using var cancellation = new CancellationTokenSource();
            _running = cancellation;
            try
            {
                CvcieProbeResult? result = await measure(next.Point, cancellation.Token);
                if (result is { } measured && !cancellation.IsCancellationRequested && next.ContentVersion == view.ContentVersion)
                    render(next.Sample, next.Point, measured);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                log.Debug("Unable to update the CVCIE probe.", error);
            }
            finally { _running = null; }
        }
    }

    public void Reset()
    {
        _latest = null;
        _running?.Cancel();
    }
}
