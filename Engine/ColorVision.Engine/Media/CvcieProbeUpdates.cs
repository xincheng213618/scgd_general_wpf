using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Threading.Tasks;

namespace ColorVision.Engine.Media;

/// <summary>Keeps one sample per tool while the current image's measurements are being prepared.</summary>
internal sealed class CvcieProbeUpdates(ImageView view, Func<ValueTask<bool>> prepare, Action<ImagePixelSample> render)
{
    private ImagePixelSample? _latest;
    private object? _pending;

    public async void Update(ImagePixelSample sample)
    {
        if (_pending != null)
        {
            _latest = sample;
            return;
        }

        ValueTask<bool> ready = prepare();
        if (ready.IsCompletedSuccessfully)
        {
            if (ready.Result) render(sample);
            return;
        }

        long contentVersion = view.ContentVersion;
        object pending = new();
        _pending = pending;
        _latest = sample;
        bool available = await ready;
        if (!ReferenceEquals(_pending, pending)) return;

        ImagePixelSample? latest = _latest;
        Reset();
        if (available && contentVersion == view.ContentVersion && latest is { } current) render(current);
    }

    public void Reset()
    {
        _pending = null;
        _latest = null;
    }
}
