#pragma warning disable CS8601
using ColorVision.Engine.Services.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    internal static class CalibrationSlotPresentation
    {
        public static string GetTitle(string key) => key switch
        {
            nameof(GroupResource.DarkNoise) => "暗噪声",
            nameof(GroupResource.DefectPoint) => "缺陷点",
            nameof(GroupResource.DSNU) => "DSNU",
            nameof(GroupResource.Uniformity) => "均匀场",
            nameof(GroupResource.ColorShift) => "色偏",
            nameof(GroupResource.Distortion) => "畸变",
            nameof(GroupResource.LineArity) => "线性度",
            nameof(GroupResource.ColorDiff) => "ColorDiff",
            nameof(GroupResource.AngleShift) => "角度偏移",
            nameof(GroupResource.Luminance) => "亮度",
            nameof(GroupResource.LumOneColor) => "单色",
            nameof(GroupResource.LumFourColor) => "四色",
            nameof(GroupResource.LumMultiColor) => "多色",
            _ => key,
        };
    }

    internal sealed class CalibrationSlotDefinition
    {
        public CalibrationSlotDefinition(
            string key,
            ServiceTypes serviceType,
            Func<GroupResource, CalibrationResource> groupGetter,
            Action<GroupResource, CalibrationResource> groupSetter,
            Func<CalibrationParam, CalibrationBase> paramGetter)
        {
            Key = key;
            ServiceType = serviceType;
            GroupGetter = groupGetter;
            GroupSetter = groupSetter;
            ParamGetter = paramGetter;
        }

        public string Key { get; }
        public ServiceTypes ServiceType { get; }
        public Func<GroupResource, CalibrationResource> GroupGetter { get; }
        public Action<GroupResource, CalibrationResource> GroupSetter { get; }
        public Func<CalibrationParam, CalibrationBase> ParamGetter { get; }
    }

    internal static class CalibrationSlotDefinitions
    {
        public static IReadOnlyList<CalibrationSlotDefinition> NormalSlots { get; } = new CalibrationSlotDefinition[]
        {
            new(nameof(GroupResource.DarkNoise), ServiceTypes.DarkNoise, group => group.DarkNoise, (group, resource) => group.DarkNoise = resource, param => param.Normal.DarkNoise),
            new(nameof(GroupResource.DefectPoint), ServiceTypes.DefectPoint, group => group.DefectPoint, (group, resource) => group.DefectPoint = resource, param => param.Normal.DefectPoint),
            new(nameof(GroupResource.DSNU), ServiceTypes.DSNU, group => group.DSNU, (group, resource) => group.DSNU = resource, param => param.Normal.DSNU),
            new(nameof(GroupResource.Uniformity), ServiceTypes.Uniformity, group => group.Uniformity, (group, resource) => group.Uniformity = resource, param => param.Normal.Uniformity),
            new(nameof(GroupResource.ColorShift), ServiceTypes.ColorShift, group => group.ColorShift, (group, resource) => group.ColorShift = resource, param => param.Normal.ColorShift),
            new(nameof(GroupResource.Distortion), ServiceTypes.Distortion, group => group.Distortion, (group, resource) => group.Distortion = resource, param => param.Normal.Distortion),
            new(nameof(GroupResource.LineArity), ServiceTypes.LineArity, group => group.LineArity, (group, resource) => group.LineArity = resource, param => param.Normal.LineArity),
            new(nameof(GroupResource.ColorDiff), ServiceTypes.ColorDiff, group => group.ColorDiff, (group, resource) => group.ColorDiff = resource, param => param.Normal.ColorDiff),
            new(nameof(GroupResource.AngleShift), ServiceTypes.AngleShift, group => group.AngleShift, (group, resource) => group.AngleShift = resource, param => param.Normal.AngleShift),
        };

        public static IReadOnlyList<CalibrationSlotDefinition> ColorSlots { get; } = new CalibrationSlotDefinition[]
        {
            new(nameof(GroupResource.Luminance), ServiceTypes.Luminance, group => group.Luminance, (group, resource) => group.Luminance = resource, param => param.Color.Luminance),
            new(nameof(GroupResource.LumOneColor), ServiceTypes.LumOneColor, group => group.LumOneColor, (group, resource) => group.LumOneColor = resource, param => param.Color.LumOneColor),
            new(nameof(GroupResource.LumFourColor), ServiceTypes.LumFourColor, group => group.LumFourColor, (group, resource) => group.LumFourColor = resource, param => param.Color.LumFourColor),
            new(nameof(GroupResource.LumMultiColor), ServiceTypes.LumMultiColor, group => group.LumMultiColor, (group, resource) => group.LumMultiColor = resource, param => param.Color.LumMultiColor),
        };

        public static IReadOnlyList<CalibrationSlotDefinition> AllSlots { get; } = NormalSlots.Concat(ColorSlots).ToArray();

        public static IReadOnlyDictionary<ServiceTypes, CalibrationSlotDefinition> ByServiceType { get; } =
            AllSlots.ToDictionary(slot => slot.ServiceType);

        public static IReadOnlyDictionary<string, CalibrationSlotDefinition> ByKey { get; } =
            AllSlots.ToDictionary(slot => slot.Key, StringComparer.Ordinal);

        public static bool TryGet(ServiceTypes serviceType, out CalibrationSlotDefinition slot)
        {
            return ByServiceType.TryGetValue(serviceType, out slot);
        }

        public static bool TryGet(string key, out CalibrationSlotDefinition slot)
        {
            return ByKey.TryGetValue(key, out slot);
        }

        public static bool IsCalibrationType(int rawType)
        {
            return TryGet((ServiceTypes)rawType, out _);
        }
    }
}
