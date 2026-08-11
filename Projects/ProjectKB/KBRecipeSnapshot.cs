namespace ProjectKB
{
    public enum KBRecipeSnapshotOrigin
    {
        CapturedAtRun,
        RebuiltFromCurrentRecipe,
    }

    /// <summary>
    /// A versioned, serialized copy of the Recipe used by a result.
    /// Rebuilt snapshots are explicitly marked because they are not proof of
    /// the thresholds that were active when a legacy result was produced.
    /// </summary>
    public sealed class KBRecipeSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string RecipeName { get; set; } = string.Empty;

        public DateTime SnapshotTime { get; set; } = DateTime.Now;

        public KBRecipeSnapshotOrigin Origin { get; set; } = KBRecipeSnapshotOrigin.CapturedAtRun;

        public KBRecipeConfig Recipe { get; set; } = new();

        public static KBRecipeSnapshot Capture(
            string recipeName,
            KBRecipeConfig recipe,
            KBRecipeSnapshotOrigin origin = KBRecipeSnapshotOrigin.CapturedAtRun,
            DateTime? snapshotTime = null)
        {
            ArgumentNullException.ThrowIfNull(recipe);

            var recipeCopy = new KBRecipeConfig();
            recipeCopy.CopyFrom(recipe);
            return new KBRecipeSnapshot
            {
                RecipeName = recipeName?.Trim() ?? string.Empty,
                SnapshotTime = snapshotTime ?? DateTime.Now,
                Origin = origin,
                Recipe = recipeCopy,
            };
        }
    }
}
