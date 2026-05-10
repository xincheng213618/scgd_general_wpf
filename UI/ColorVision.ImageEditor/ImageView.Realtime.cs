using ColorVision.ImageEditor.Realtime;

namespace ColorVision.ImageEditor
{
    public partial class ImageView
    {
        private RealtimeImageViewService? _realtime;

        public RealtimeImageViewService Realtime => _realtime ??= new RealtimeImageViewService(this);
    }
}
