namespace ColorVision.UI.LogImp
{
    /// <summary>Adds a redacted copy of the active application configuration to feedback packages.</summary>
    public sealed class ConfigurationSnapshotCollector : IFeedbackLogCollector
    {
        public string Name => "Configuration Snapshot";
        public string Description => "Active ColorVision configuration with credentials and tokens redacted";
        public int Order => 15;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles() =>
            FeedbackConfigurationSnapshot.CollectFiles([("Config/ColorVisionConfig.json", ConfigHandler.GetInstance().ConfigFilePath)]);
    }
}
