using ColorVision.Algorithms;
using System;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Strict preset boundary for registration V1.</summary>
    public static class ImageRegistrationPresetSerializer
    {
        public static string Serialize(string presetId, ImageRegistrationParameters parameters)
            => Serialize(ImageAlgorithmPlatform.Catalog, presetId, parameters);

        public static string Serialize(IAlgorithmCatalog catalog, string presetId, ImageRegistrationParameters parameters)
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

        public static (string PresetId, ImageRegistrationParameters Parameters) Deserialize(string json)
            => Deserialize(ImageAlgorithmPlatform.Catalog, json);

        public static (string PresetId, ImageRegistrationParameters Parameters) Deserialize(IAlgorithmCatalog catalog, string json)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            AlgorithmDescriptor descriptor = ResolveDescriptor(catalog);
            return StrictAlgorithmParameterPresetSerializer.Deserialize<ImageRegistrationParameters>(json, descriptor);
        }

        private static AlgorithmDescriptor ResolveDescriptor(IAlgorithmCatalog catalog)
            => StandardAlgorithmAdapterContract.ResolveCompatible<ImageRegistrationParameters>(
                catalog,
                StandardAlgorithmIds.ImageRegistration,
                "image-registration");
    }
}
