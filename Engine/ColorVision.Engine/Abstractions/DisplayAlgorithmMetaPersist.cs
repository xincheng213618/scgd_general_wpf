namespace ColorVision.Engine
{
    /// <summary>
    /// Represents the persisted data structure for DisplayAlgorithmMeta.
    /// Used for JSON serialization/deserialization.
    /// </summary>
    public class DisplayAlgorithmMetaPersist
    {
        public string Name { get; set; }
        public string AlgorithmTypeFullName { get; set; }
        public string ConfigJson { get; set; }
        public string Tag { get; set; }
    }
}
