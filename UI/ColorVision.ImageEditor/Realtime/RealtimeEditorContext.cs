using System;
using System.Windows;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeEditorContext
    {
        public RealtimeEditorContext(FrameworkElement host, RealtimeImageViewService realtime)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        }

        public FrameworkElement Host { get; }

        public RealtimeImageViewService Realtime { get; }

        public Window? GetOwnerWindow()
        {
            return Window.GetWindow(Host) ?? Application.Current?.MainWindow;
        }
    }
}