using ColorVision.Algorithms;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Loads persisted M3 parameters into the M4 schema while retaining every M3 value and applying M4 defaults.</summary>
    internal sealed class ImageComparisonParametersV1ToV2Migrator : IAlgorithmParameterMigrator
    {
        public AlgorithmId AlgorithmId => StandardAlgorithmIds.ImageComparison;

        public int FromSchemaVersion => 1;

        public int ToSchemaVersion => 2;

        public JsonElement Migrate(JsonElement parameters)
        {
            ImageComparisonParameters migrated = parameters.Deserialize<ImageComparisonParameters>(AlgorithmJson.Options)
                ?? throw new JsonException("Could not migrate ImageComparisonParameters schema 1.");
            return AlgorithmJson.ToElement(migrated);
        }
    }
}
