using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons.AAFindPoints;
using ColorVision.Engine.Templates.Jsons.BinocularFusion;
using ColorVision.Engine.Templates.Jsons.DetectScreenDefects;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using ColorVision.Engine.Templates.Jsons.FindCross;
using ColorVision.Engine.Templates.Jsons.FOV2;
using ColorVision.Engine.Templates.Jsons.Ghost2;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.SFR2;
using ColorVision.Engine.Templates.Jsons.SFRFindROI;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Node.Algorithm;
using FlowEngineLib.Node.OLED;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.Engine.PropertyEditor;

public sealed class AlgorithmTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => obj switch
    {
        AlgorithmNode node => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, node,
            () => GetTemplate(node.Algorithm), nameof(node.Algorithm), hasDirectTemplateEditor: false, allowManualEntryWithoutTemplate: true),
        Algorithm2InNode node => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, node,
            () => GetTemplate(node.Algorithm), nameof(node.Algorithm), hasDirectTemplateEditor: false),
        AlgorithmARVRNode node => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, node,
            () => GetTemplate(node.Algorithm), nameof(node.Algorithm), hasDirectTemplateEditor: false),
        AlgorithmGhostV2Node => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateGhostQK()),
        _ => throw new ArgumentException("Unsupported algorithm node.", nameof(obj))
    };

    private static ITemplate? GetTemplate(AlgorithmType algorithm) => algorithm switch
    {
        AlgorithmType.MTF => new TemplateMTF2(),
        AlgorithmType.SFR => new TemplateSFR2(),
        AlgorithmType.FOV => new TemplateDFOV(),
        AlgorithmType.鬼影 => new TemplateGhostQK(),
        AlgorithmType.畸变 => new TemplateDistortion2(),
        AlgorithmType.SFR_FindROI => new TemplateSFRFindROI(),
        AlgorithmType.双目融合 => new TemplateBinocularFusion(),
        AlgorithmType.AA布点 => new TemplateAAFindPoints(),
        AlgorithmType.十字计算 => new TemplateFindCross(),
        _ => null
    };

    private static ITemplate? GetTemplate(Algorithm2Type algorithm) => algorithm switch
    {
        Algorithm2Type.MTF => new TemplateMTF2(),
        Algorithm2Type.SFR => new TemplateSFR2(),
        Algorithm2Type.SFR_FindROI => new TemplateSFRFindROI(),
        Algorithm2Type.十字计算 => new TemplateFindCross(),
        _ => null
    };

    private static ITemplate? GetTemplate(AlgorithmARVRType algorithm) => algorithm switch
    {
        AlgorithmARVRType.MTF => new TemplateMTF2(),
        AlgorithmARVRType.SFR => new TemplateSFR2(),
        AlgorithmARVRType.FOV => new TemplateDFOV(),
        AlgorithmARVRType.畸变 => new TemplateDistortion2(),
        AlgorithmARVRType.SFR_FindROI => new TemplateSFRFindROI(),
        AlgorithmARVRType.双目融合 => new TemplateBinocularFusion(),
        AlgorithmARVRType.十字计算 => new TemplateFindCross(),
        AlgorithmARVRType.屏幕缺陷检测 => new TemplateDetectScreenDefects(),
        _ => null
    };
}

public sealed class AiAlgorithmOperatorPropertiesEditor : EnumPropertiesEditor
{
    protected override IEnumerable<Enum> GetValues(Type enumType) => base.GetValues(enumType).Where(value =>
        value is not (AlgorithmType.灯珠检测 or AlgorithmType.灯带检测 or Algorithm2Type.JND or Algorithm2Type.灯带检测));
}
