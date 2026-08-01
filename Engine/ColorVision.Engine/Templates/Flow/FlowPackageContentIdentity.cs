using ColorVision.Database;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// Builds a deployment-neutral identity for an associated template.
    /// Database ids and the template's own name are intentionally excluded,
    /// while nested template references and effective parameter values remain.
    /// </summary>
    internal static class FlowPackageContentIdentity
    {
        private static readonly HashSet<string> RootIdentityProperties =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Id",
                "Name",
                "ModMaster",
                "ModDetailModels",
                "CreateCommand",
            };

        public static FlowPackageTemplatePayload CreatePayload(
            FlowPackageTemplate template)
        {
            ArgumentNullException.ThrowIfNull(template);
            return new FlowPackageTemplatePayload
            {
                SerializedContent = template.SerializedContent,
                Details = CloneDetails(template.Details),
            };
        }

        public static string SerializePayload(
            FlowPackageTemplatePayload payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            return JsonConvert.SerializeObject(
                payload,
                Formatting.Indented);
        }

        public static string ComputePayloadHash(string payloadJson)
        {
            ArgumentNullException.ThrowIfNull(payloadJson);
            return ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
        }

        public static string ComputeContentHash(
            string templateCode,
            string? serializedContent,
            IReadOnlyList<FlowPackageDetailItem>? details,
            IReadOnlyDictionary<string, string>? nameMap = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateCode);
            JObject signature = CreateSignature(
                templateCode,
                serializedContent,
                details,
                nameMap);
            string canonical = signature.ToString(Formatting.None);
            return ComputeHash(Encoding.UTF8.GetBytes(canonical));
        }

        public static string ComputeContentHash(
            string templateCode,
            object templateValue,
            int templateDictionaryId)
        {
            ArgumentNullException.ThrowIfNull(templateValue);
            return ComputeContentHash(
                templateCode,
                templateValue is ParamModBase
                    ? null
                    : JsonConvert.SerializeObject(
                        templateValue,
                        Formatting.None),
                ExtractDetails(
                    templateValue,
                    templateDictionaryId));
        }

        public static bool IsEquivalent(
            string templateCode,
            object existingTemplateValue,
            FlowPackageTemplate packageTemplate,
            IReadOnlyDictionary<string, string>? nameMap,
            int templateDictionaryId)
        {
            ArgumentNullException.ThrowIfNull(existingTemplateValue);
            ArgumentNullException.ThrowIfNull(packageTemplate);
            if (string.IsNullOrWhiteSpace(
                    packageTemplate.SerializedContent)
                && (packageTemplate.Details == null
                    || packageTemplate.Details.Count == 0))
            {
                return false;
            }

            string existingHash = ComputeContentHash(
                templateCode,
                existingTemplateValue,
                templateDictionaryId);
            string packageHash = ComputeContentHash(
                templateCode,
                packageTemplate.SerializedContent,
                packageTemplate.Details,
                nameMap);
            return string.Equals(
                existingHash,
                packageHash,
                StringComparison.OrdinalIgnoreCase);
        }

        private static JObject CreateSignature(
            string templateCode,
            string? serializedContent,
            IReadOnlyList<FlowPackageDetailItem>? details,
            IReadOnlyDictionary<string, string>? nameMap)
        {
            Dictionary<string, string> replacements =
                nameMap == null
                    ? new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(
                        nameMap,
                        StringComparer.OrdinalIgnoreCase);
            string? adjustedContent =
                FlowPackageHelper.ReplaceTemplateReferencesInJsonContent(
                    serializedContent,
                    replacements);
            return new JObject
            {
                ["templateCode"] = templateCode,
                ["content"] = NormalizeSerializedContent(adjustedContent),
                ["details"] = NormalizeDetails(details, replacements),
            };
        }

        private static JToken NormalizeSerializedContent(
            string? serializedContent)
        {
            if (string.IsNullOrWhiteSpace(serializedContent))
                return JValue.CreateNull();

            try
            {
                return NormalizeToken(
                    JToken.Parse(serializedContent),
                    isRoot: true);
            }
            catch (JsonException)
            {
                return new JValue(serializedContent);
            }
        }

        private static JArray NormalizeDetails(
            IReadOnlyList<FlowPackageDetailItem>? details,
            Dictionary<string, string> nameMap)
        {
            if (details == null || details.Count == 0)
                return new JArray();

            IEnumerable<JObject> normalized = details
                .Select(detail => new JObject
                {
                    ["key"] = GetStableDetailKey(detail),
                    ["valueA"] =
                        FlowPackageHelper
                            .IsTemplateReferenceProperty(
                                detail.Symbol)
                            ? FlowPackageHelper
                                .ReplaceTemplateReferencesInString(
                                    detail.ValueA,
                                    nameMap)
                            : detail.ValueA,
                })
                .OrderBy(
                    item => item["key"]?.Value<string>(),
                    StringComparer.Ordinal)
                .ThenBy(
                    item => item["valueA"]?.Value<string>(),
                    StringComparer.Ordinal);
            return new JArray(normalized);
        }

        internal static string? GetStableDetailKey(
            FlowPackageDetailItem detail)
        {
            ArgumentNullException.ThrowIfNull(detail);
            if (!string.IsNullOrWhiteSpace(detail.Symbol))
                return "symbol:" + detail.Symbol;
            if (detail.AddressCode.HasValue)
            {
                return "address:"
                    + detail.AddressCode.Value.ToString(
                        CultureInfo.InvariantCulture);
            }
            if (detail.SysPid > 0)
            {
                return "legacy-id:"
                    + detail.SysPid.ToString(
                        CultureInfo.InvariantCulture);
            }
            return null;
        }

        private static JToken NormalizeToken(
            JToken token,
            bool isRoot = false)
        {
            if (token is JObject objectToken)
            {
                var normalized = new JObject();
                foreach (JProperty property in objectToken
                    .Properties()
                    .Where(property =>
                        !isRoot
                        || !RootIdentityProperties.Contains(
                            property.Name))
                    .OrderBy(
                        property => property.Name,
                        StringComparer.Ordinal))
                {
                    normalized.Add(
                        property.Name,
                        NormalizeToken(property.Value));
                }
                return normalized;
            }

            if (token is JArray arrayToken)
            {
                return new JArray(
                    arrayToken.Select(item => NormalizeToken(item)));
            }

            if (token.Type == JTokenType.String)
            {
                string? value = token.Value<string>();
                if (!string.IsNullOrWhiteSpace(value)
                    && LooksLikeJson(value))
                {
                    try
                    {
                        return new JValue(
                            NormalizeToken(JToken.Parse(value))
                                .ToString(Formatting.None));
                    }
                    catch (JsonException)
                    {
                    }
                }
            }

            return token.DeepClone();
        }

        private static bool LooksLikeJson(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.Length < 2)
                return false;
            return (trimmed[0] == '{' && trimmed[^1] == '}')
                || (trimmed[0] == '[' && trimmed[^1] == ']');
        }

        private static IReadOnlyList<FlowPackageDetailItem>
            ExtractDetails(
                object templateValue,
                int templateDictionaryId)
        {
            if (templateValue is not ParamModBase param)
                return Array.Empty<FlowPackageDetailItem>();

            var details = new List<ModDetailModel>();
            param.GetDetail(details);
            Dictionary<int, SysDictionaryModDetaiModel> definitions =
                SysDictionaryModDetailDao.Instance
                    .GetAllByPid(templateDictionaryId)
                    .ToDictionary(definition => definition.Id);
            return details.Select(detail =>
            {
                definitions.TryGetValue(
                    detail.SysPid,
                    out SysDictionaryModDetaiModel? definition);
                return new FlowPackageDetailItem
                {
                    SysPid = definition == null
                        ? detail.SysPid
                        : 0,
                    Symbol = definition?.Symbol,
                    AddressCode = definition?.AddressCode,
                    ValueA = detail.ValueA,
                    ValueB = detail.ValueB,
                    IsEnable = detail.IsEnable,
                    IsDelete = detail.IsDelete,
                };
            }).ToArray();
        }

        internal static List<FlowPackageDetailItem> CloneDetails(
            IEnumerable<FlowPackageDetailItem>? details)
        {
            if (details == null)
                return new List<FlowPackageDetailItem>();
            return details.Select(detail =>
                new FlowPackageDetailItem
                {
                    SysPid = detail.SysPid,
                    Symbol = detail.Symbol,
                    AddressCode = detail.AddressCode,
                    ValueA = detail.ValueA,
                    ValueB = detail.ValueB,
                    IsEnable = detail.IsEnable,
                    IsDelete = detail.IsDelete,
                }).ToList();
        }

        private static string ComputeHash(byte[] data)
        {
            return Convert.ToHexString(SHA256.HashData(data))
                .ToLowerInvariant();
        }
    }
}
