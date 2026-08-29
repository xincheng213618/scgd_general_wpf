using ColorVision.Algorithms;
using System;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Strict V1 preset boundary for the geometric-transform parameter contract.</summary>
    public static class GeometricTransformPresetSerializer
    {
        public static string Serialize(string presetId, GeometricTransformParameters parameters)
            => Serialize(ImageAlgorithmPlatform.Catalog, presetId, parameters);

        public static string Serialize(IAlgorithmCatalog catalog, string presetId, GeometricTransformParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentException.ThrowIfNullOrWhiteSpace(presetId);
            ArgumentNullException.ThrowIfNull(parameters);
            AlgorithmValidationResult validation = parameters.Validate();
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("; ", validation.Issues));
            AlgorithmDescriptor descriptor = ResolveDescriptor(catalog);
            AlgorithmParameterPreset preset = AlgorithmParameterPreset.Create(presetId, descriptor.Id, descriptor.Version, parameters);
            return JsonSerializer.Serialize(preset, AlgorithmJson.Options);
        }

        public static (string PresetId, GeometricTransformParameters Parameters) Deserialize(string json)
            => Deserialize(ImageAlgorithmPlatform.Catalog, json);

        public static (string PresetId, GeometricTransformParameters Parameters) Deserialize(IAlgorithmCatalog catalog, string json)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            AlgorithmDescriptor descriptor = ResolveDescriptor(catalog);
            return StrictAlgorithmParameterPresetSerializer.Deserialize<GeometricTransformParameters>(json, descriptor);
        }

        private static AlgorithmDescriptor ResolveDescriptor(IAlgorithmCatalog catalog)
            => StandardAlgorithmAdapterContract.ResolveCompatible<GeometricTransformParameters>(
                catalog,
                StandardAlgorithmIds.GeometricTransform,
                "geometric-transform");
    }
}
