using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ColorVision.UI.PropertyEditor.Json
{
    internal sealed class JsonEditorSchemaDocument
    {
        private readonly Dictionary<string, JsonEditorSchemaNode> _nodes;

        private JsonEditorSchemaDocument(
            string title,
            string description,
            bool providerMaintained,
            string sourceSummary,
            Dictionary<string, JsonEditorSchemaNode> nodes)
        {
            Title = title;
            Description = description;
            ProviderMaintained = providerMaintained;
            SourceSummary = sourceSummary;
            _nodes = nodes;
        }

        public string Title { get; }

        public string Description { get; }

        public bool ProviderMaintained { get; }

        public string SourceSummary { get; }

        public int FieldCount => _nodes.Count;

        public int DescribedFieldCount => _nodes.Values.Count(node => !string.IsNullOrWhiteSpace(node.Description));

        public static JsonEditorSchemaDocument? TryParse(string? schemaJson, string? fallbackTitle = null)
        {
            if (string.IsNullOrWhiteSpace(schemaJson))
                return null;

            try
            {
                var root = JObject.Parse(schemaJson);
                var nodes = new Dictionary<string, JsonEditorSchemaNode>(StringComparer.OrdinalIgnoreCase);
                IndexProperties(root, "$", nodes);

                var title = ReadString(root, "title");
                var description = ReadString(root, "description");
                var providerMaintained = root.SelectToken("x-colorvision.providerMaintained")?.Value<bool?>() == true;
                var sourceSummary = BuildSourceSummary(root);

                return new JsonEditorSchemaDocument(
                    string.IsNullOrWhiteSpace(title) ? fallbackTitle ?? "JSON Schema" : title,
                    description ?? string.Empty,
                    providerMaintained,
                    sourceSummary,
                    nodes);
            }
            catch
            {
                return null;
            }
        }

        public JsonEditorSchemaNode? FindNode(string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(propertyPath))
                return null;

            var jsonPath = propertyPath.StartsWith("$.", StringComparison.Ordinal)
                ? propertyPath
                : "$." + propertyPath;

            if (_nodes.TryGetValue(jsonPath, out var node))
                return node;

            var arrayWildcardPath = Regex.Replace(jsonPath, @"\[\d+\]", "[]");
            if (_nodes.TryGetValue(arrayWildcardPath, out node))
                return node;

            var slashArrayWildcardPath = Regex.Replace(jsonPath, @"\[\d+\]\.", "[]/");
            return _nodes.TryGetValue(slashArrayWildcardPath, out node) ? node : null;
        }

        private static void IndexProperties(JObject schemaNode, string currentPath, Dictionary<string, JsonEditorSchemaNode> nodes)
        {
            if (schemaNode["properties"] is not JObject properties)
                return;

            foreach (var property in properties.Properties())
            {
                if (property.Value is not JObject propertySchema)
                    continue;

                var propertyPath = currentPath == "$"
                    ? "$." + property.Name
                    : currentPath + "." + property.Name;

                AddNode(nodes, propertyPath, propertySchema);
                IndexProperties(propertySchema, propertyPath, nodes);

                if (propertySchema["items"] is JObject itemSchema)
                {
                    var arrayPath = propertyPath + "[]";
                    AddNode(nodes, arrayPath, itemSchema);

                    if (itemSchema["properties"] is JObject itemProperties)
                    {
                        foreach (var itemProperty in itemProperties.Properties())
                        {
                            if (itemProperty.Value is not JObject itemPropertySchema)
                                continue;

                            var itemPropertyPath = arrayPath + "." + itemProperty.Name;
                            AddNode(nodes, itemPropertyPath, itemPropertySchema);
                            AddNode(nodes, arrayPath + "/" + itemProperty.Name, itemPropertySchema);
                            IndexProperties(itemPropertySchema, itemPropertyPath, nodes);
                        }
                    }
                }
            }
        }

        private static void AddNode(Dictionary<string, JsonEditorSchemaNode> nodes, string fallbackPath, JObject schema)
        {
            var node = JsonEditorSchemaNode.From(schema, fallbackPath);
            nodes[fallbackPath] = node;

            if (!string.IsNullOrWhiteSpace(node.JsonPath))
                nodes[node.JsonPath] = node;
        }

        private static string BuildSourceSummary(JObject root)
        {
            var sourceSet = root.SelectToken("x-colorvision.providerReference.sourceSet")?.Value<string>();
            var folder = root.SelectToken("x-colorvision.providerReference.testCaseFolder")?.Value<string>();
            var defaultParam = root.SelectToken("x-colorvision.providerReference.defaultParamFile")?.Value<string>();
            if (!string.IsNullOrWhiteSpace(sourceSet) && !string.IsNullOrWhiteSpace(folder))
                return string.IsNullOrWhiteSpace(defaultParam)
                    ? $"{sourceSet}/{folder}"
                    : $"{sourceSet}/{folder}/{defaultParam}";

            var code = root.SelectToken("x-colorvision.source.code")?.Value<string>();
            return string.IsNullOrWhiteSpace(code) ? string.Empty : code;
        }

        private static string? ReadString(JObject obj, string propertyName)
        {
            return obj.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var token)
                ? token.Value<string>()
                : null;
        }
    }

    internal sealed class JsonEditorSchemaNode
    {
        private JsonEditorSchemaNode(
            string jsonPath,
            string title,
            string description,
            string unit,
            double? minimum,
            double? maximum,
            IReadOnlyList<JsonEditorSchemaEnumItem> enumItems,
            IReadOnlyList<string> examples)
        {
            JsonPath = jsonPath;
            Title = title;
            Description = description;
            Unit = unit;
            Minimum = minimum;
            Maximum = maximum;
            EnumItems = enumItems;
            Examples = examples;
        }

        public string JsonPath { get; }

        public string Title { get; }

        public string Description { get; }

        public string Unit { get; }

        public double? Minimum { get; }

        public double? Maximum { get; }

        public IReadOnlyList<JsonEditorSchemaEnumItem> EnumItems { get; }

        public IReadOnlyList<string> Examples { get; }

        public bool HasEnum => EnumItems.Count > 0;

        public bool HasRange => Minimum.HasValue || Maximum.HasValue;

        public static JsonEditorSchemaNode From(JObject schema, string fallbackPath)
        {
            var enumItems = BuildEnumItems(schema);
            var examples = schema["examples"] is JArray examplesArray
                ? examplesArray.Select(FormatToken).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray()
                : Array.Empty<string>();

            return new JsonEditorSchemaNode(
                schema.SelectToken("x-provider.jsonPath")?.Value<string>() ?? fallbackPath,
                schema["title"]?.Value<string>() ?? string.Empty,
                schema["description"]?.Value<string>() ?? string.Empty,
                schema["unit"]?.Value<string>() ?? string.Empty,
                schema["minimum"]?.Value<double?>(),
                schema["maximum"]?.Value<double?>(),
                enumItems,
                examples);
        }

        public string GetTitleOrFallback(string fallback)
        {
            return string.IsNullOrWhiteSpace(Title) ? fallback : Title;
        }

        public string BuildHint(string propertyPath)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Description))
                parts.Add(Description);

            var range = BuildRangeText();
            if (!string.IsNullOrWhiteSpace(range))
                parts.Add(range);

            if (!string.IsNullOrWhiteSpace(Unit))
                parts.Add($"单位: {Unit}");

            if (Examples.Count > 0)
                parts.Add($"示例: {string.Join(", ", Examples)}");

            if (EnumItems.Count > 0)
                parts.Add("可选值: " + string.Join("; ", EnumItems.Select(item => item.DisplayText)));

            parts.Add(string.IsNullOrWhiteSpace(JsonPath) ? propertyPath : JsonPath);
            return string.Join(Environment.NewLine, parts);
        }

        public string BuildRangeText()
        {
            if (Minimum.HasValue && Maximum.HasValue)
                return $"范围: {FormatNumber(Minimum.Value)} - {FormatNumber(Maximum.Value)}";

            if (Minimum.HasValue)
                return $"最小值: {FormatNumber(Minimum.Value)}";

            if (Maximum.HasValue)
                return $"最大值: {FormatNumber(Maximum.Value)}";

            return string.Empty;
        }

        public bool IsInRange(double value)
        {
            if (Minimum.HasValue && value < Minimum.Value)
                return false;

            if (Maximum.HasValue && value > Maximum.Value)
                return false;

            return true;
        }

        public bool Matches(string filterText)
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            return Contains(Title, filterText)
                || Contains(Description, filterText)
                || Contains(Unit, filterText)
                || EnumItems.Any(item => Contains(item.DisplayText, filterText));
        }

        private static IReadOnlyList<JsonEditorSchemaEnumItem> BuildEnumItems(JObject schema)
        {
            if (schema["enum"] is not JArray enumArray)
                return Array.Empty<JsonEditorSchemaEnumItem>();

            var descriptions = schema["x-enumDescriptions"] as JArray;
            var items = new List<JsonEditorSchemaEnumItem>();
            for (var i = 0; i < enumArray.Count; i++)
            {
                var value = enumArray[i];
                var description = descriptions != null && i < descriptions.Count
                    ? descriptions[i]?.Value<string>() ?? string.Empty
                    : string.Empty;
                items.Add(new JsonEditorSchemaEnumItem(value.DeepClone(), description));
            }

            return items;
        }

        private static bool Contains(string? value, string filterText)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(filterText, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatToken(JToken token)
        {
            return token.Type == JTokenType.String
                ? token.Value<string>() ?? string.Empty
                : token.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("G", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class JsonEditorSchemaEnumItem
    {
        public JsonEditorSchemaEnumItem(JToken value, string description)
        {
            Value = value;
            Description = description;
            DisplayText = string.IsNullOrWhiteSpace(description)
                ? ValueText
                : $"{ValueText} - {description}";
        }

        public JToken Value { get; }

        public string Description { get; }

        public string ValueText => Value.Type == JTokenType.String
            ? Value.Value<string>() ?? string.Empty
            : Value.ToString(Newtonsoft.Json.Formatting.None);

        public string DisplayText { get; }

        public object? ToObject()
        {
            return Value is JValue jValue ? jValue.Value : Value.ToObject<object>();
        }

        public bool Matches(JToken token)
        {
            return JToken.DeepEquals(Value, token);
        }
    }
}
