using ColorVision.Algorithms;
using System;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Strict V1 preset boundary for calibrated lens-distortion correction.</summary>
    public static class LensDistortionCorrectionPresetSerializer
    {
        public static string Serialize(string presetId, LensDistortionCorrectionParameters parameters)
            => Serialize(ImageAlgorithmPlatform.Catalog, presetId, parameters);

        public static string Serialize(IAlgorithmCatalog catalog, string presetId, LensDistortionCorrectionParameters parameters)
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

        public static (string PresetId, LensDistortionCorrectionParameters Parameters) Deserialize(string json)
            => Deserialize(ImageAlgorithmPlatform.Catalog, json);

        public static (string PresetId, LensDistortionCorrectionParameters Parameters) Deserialize(IAlgorithmCatalog catalog, string json)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            AlgorithmDescriptor descriptor = ResolveDescriptor(catalog);
            return StrictAlgorithmParameterPresetSerializer.Deserialize<LensDistortionCorrectionParameters>(json, descriptor);
        }

        private static AlgorithmDescriptor ResolveDescriptor(IAlgorithmCatalog catalog)
            => StandardAlgorithmAdapterContract.ResolveCompatible<LensDistortionCorrectionParameters>(
                catalog,
                StandardAlgorithmIds.LensDistortionCorrection,
                "lens-distortion-correction");
    }
}
