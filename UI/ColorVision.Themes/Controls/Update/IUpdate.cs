namespace ColorVision.Themes.Controls
{
    public interface IUpdate
    {
        public string DownloadTile { get; set; }
        public int ProgressValue { get; set; }

        public string SpeedValue { get; set; }

        public string RemainingTimeValue { get; set; }
    }
}
