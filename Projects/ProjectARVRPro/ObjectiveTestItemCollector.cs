using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;

namespace ProjectARVRPro
{
    public static class ObjectiveTestItemCollector
    {
        public static IReadOnlyList<ObjectiveTestItem> CollectFromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<ObjectiveTestItem>();

            var items = new List<ObjectiveTestItem>();
            CollectToken(JToken.Parse(json), items);
            return items;
        }

        private static void CollectToken(JToken token, ICollection<ObjectiveTestItem> items)
        {
            if (token is JArray array)
            {
                foreach (var child in array)
                    CollectToken(child, items);
                return;
            }

            if (token is not JObject obj)
                return;

            if (obj.ContainsKey("Name") && obj.ContainsKey("Value"))
            {
                try
                {
                    var testItem = obj.ToObject<ObjectiveTestItem>();
                    if (testItem != null)
                    {
                        items.Add(testItem);
                        return;
                    }
                }
                catch
                {
                    // A malformed lookalike should not hide valid nested result items.
                }
            }

            if (obj.ContainsKey("Name") && obj.ContainsKey("Y"))
            {
                try
                {
                    var poiData = obj.ToObject<PoixyuvData>();
                    if (poiData != null)
                    {
                        AddPoiItems(poiData, items);
                        return;
                    }
                }
                catch
                {
                    // Continue into child objects when this is not a valid POI record.
                }
            }

            foreach (var property in obj.Properties())
                CollectToken(property.Value, items);
        }

        private static void AddPoiItems(PoixyuvData poiData, ICollection<ObjectiveTestItem> items)
        {
            AddPoiItem(items, $"{poiData.Name}(Lv)", poiData.Y, "cd/m2");
            AddPoiItem(items, $"{poiData.Name}(Cx)", poiData.x);
            AddPoiItem(items, $"{poiData.Name}(Cy)", poiData.y);
            AddPoiItem(items, $"{poiData.Name}(u')", poiData.u);
            AddPoiItem(items, $"{poiData.Name}(v')", poiData.v);
        }

        private static void AddPoiItem(ICollection<ObjectiveTestItem> items, string name, double value, string unit = "")
        {
            items.Add(new ObjectiveTestItem
            {
                Name = name,
                Value = value,
                TestValue = value.ToString("F4"),
                Unit = unit,
                LowLimit = 0,
                UpLimit = 0,
            });
        }
    }
}
