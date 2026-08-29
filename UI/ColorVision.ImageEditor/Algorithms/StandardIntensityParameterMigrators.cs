using ColorVision.Algorithms;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    internal sealed class ThresholdParametersV1ToV2Migrator : IAlgorithmParameterMigrator
    {
        public AlgorithmId AlgorithmId => StandardAlgorithmIds.Threshold;

        public int FromSchemaVersion => 1;

        public int ToSchemaVersion => 2;

        public JsonElement Migrate(JsonElement parameters)
        {
            ThresholdParameters migrated = parameters.Deserialize<ThresholdParameters>(AlgorithmJson.Options)
                ?? throw new JsonException("Could not migrate ThresholdParameters schema 1.");
            migrated.UseNominalRange = false;
            return AlgorithmJson.ToElement(migrated);
        }
    }

    internal sealed class DenoiseParametersV1ToV2Migrator : IAlgorithmParameterMigrator
    {
        public AlgorithmId AlgorithmId => StandardAlgorithmIds.Denoise;

        public int FromSchemaVersion => 1;

        public int ToSchemaVersion => 2;

        public JsonElement Migrate(JsonElement parameters)
        {
            DenoiseParameters migrated = parameters.Deserialize<DenoiseParameters>(AlgorithmJson.Options)
                ?? throw new JsonException("Could not migrate DenoiseParameters schema 1.");
            migrated.UseNominalColorSigma = false;
            return AlgorithmJson.ToElement(migrated);
        }
    }
}
