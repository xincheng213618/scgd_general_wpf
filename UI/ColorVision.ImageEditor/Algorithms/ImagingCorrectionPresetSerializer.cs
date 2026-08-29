using ColorVision.Algorithms;
using System;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Strict V1 preset boundary, including host reference locators and calibration provenance.</summary>
    public static class ImagingCorrectionPresetSerializer
    {
        public static string Serialize(string presetId, ImagingCorrectionParameters parameters)
            => Serialize(ImageAlgorithmPlatform.Catalog, presetId, parameters);

        public static string Serialize(IAlgorithmCatalog catalog, string presetId, ImagingCorrectionParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentException.ThrowIfNullOrWhiteSpace(presetId);
            ArgumentNullException.ThrowIfNull(parameters);
            AlgorithmValidationResult validation = parameters.Validate();
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("; ", validation.Issues));
            AlgorithmDescriptor descriptor = ResolveDescriptor(catalog);
            return JsonSerializer.Serialize(
                AlgorithmParameterPreset.Create(presetId, descriptor.Id, descriptor.Version, parameters),
                AlgorithmJson.Options);
        }

        public static (string PresetId, ImagingCorrectionParameters Parameters) Deserialize(string json)
            => Deserialize(ImageAlgorithmPlatform.Catalog, json);

        public static (string PresetId, ImagingCorrectionParameters Parameters) Deserialize(IAlgorithmCatalog catalog, string json)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            AlgorithmDescriptor descriptor = ResolveDescriptor(catalog);
            return StrictAlgorithmParameterPresetSerializer.Deserialize<ImagingCorrectionParameters>(json, descriptor);
        }

        private static AlgorithmDescriptor ResolveDescriptor(IAlgorithmCatalog catalog)
            => StandardAlgorithmAdapterContract.ResolveCompatible<ImagingCorrectionParameters>(
                catalog,
                StandardAlgorithmIds.ImagingCorrection,
                "imaging-correction");
    }
}
