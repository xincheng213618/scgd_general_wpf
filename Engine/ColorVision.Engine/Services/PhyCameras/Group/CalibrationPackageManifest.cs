using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    internal sealed class CalibrationPackageManifest
    {
        private readonly ILookup<string, ZipCalibrationItem> _itemsByTitle;

        public CalibrationPackageManifest(IEnumerable<ZipCalibrationItem> items)
        {
            Items = items.DistinctBy(item => item.ResourceKey).ToArray();
            _itemsByTitle = Items.ToLookup(item => item.Title, StringComparer.Ordinal);
        }

        public IReadOnlyList<ZipCalibrationItem> Items { get; }

        public ZipCalibrationItem? ResolveGroupItem(ZipCalibrationItem reference)
        {
            CalibrationPackageResourceKey key = reference.ResourceKey;
            IEnumerable<ZipCalibrationItem> candidates = _itemsByTitle[key.Title];
            ZipCalibrationItem? exactMatch = candidates.FirstOrDefault(item => item.ResourceKey == key);
            if (exactMatch != null)
                return exactMatch;

            // Older group configurations may only identify a resource by its title.
            ZipCalibrationItem[] sameType = candidates.Where(item => item.ResourceKey.Type == key.Type).Take(2).ToArray();
            if (sameType.Length == 1)
                return sameType[0];
            return sameType.Length == 0 && candidates.Count() == 1 ? candidates.First() : null;
        }

        public SysResourceModel? FindExistingResource(ZipCalibrationItem item, IReadOnlyList<SysResourceModel> candidates, string fileName)
        {
            SysResourceModel? exactMatch = candidates.FirstOrDefault(resource =>
                string.Equals(resource.Value, fileName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;

            // Preserve title-based replacement for a single unambiguous resource.
            var type = item.ResourceKey.Type;
            return candidates.Count == 1 && _itemsByTitle[item.Title].Count(entry => entry.ResourceKey.Type == type) == 1
                ? candidates[0]
                : null;
        }
    }
}
