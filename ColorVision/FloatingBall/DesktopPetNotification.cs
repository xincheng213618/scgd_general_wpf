namespace ColorVision.FloatingBall
{
    public enum DesktopPetNotificationKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class DesktopPetNotification
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DesktopPetNotificationKind Kind { get; set; } = DesktopPetNotificationKind.Info;
    }
}
