using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Templates.DataLoad;
using ColorVision.Engine.Templates.ImageCropping;
using ColorVision.Engine.Templates.Jsons.AutoExpTime;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Engine.Templates.Jsons.ImageROI;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.Jsons.PoiAnalysis;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIGenCali;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIRevise;
using FlowEngineLib.Base;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;

namespace ColorVision.Engine.PropertyEditor;

public sealed class CalibrationTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(
        property, obj, () => FlowNodePropertyEditorRegistration.CreateCalibrationTemplate(obj), nameof(CVCommonNode.DeviceCode), hasDirectTemplateEditor: true);
}

public sealed class PoiTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplatePoi());
}

public sealed class PoiFilterTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplatePoiFilterParam());
}

public sealed class PoiReviseTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplatePoiReviseParam());
}

public sealed class AutoExposureTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateMultiTemplateEditor(property, obj, new TemplateAutoExpTimeV2(), new TemplateAutoExpTime());
}

public sealed class CameraRunTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateCameraRunParam());
}

public sealed class AutoFocusTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateAutoFocus());
}

public sealed class BuildPoiTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateBuildPoi());
}

public sealed class PoiOutputTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplatePoiOutputParam());
}

public sealed class PoiGenCaliTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplatePoiGenCalParam());
}

public sealed class SmuTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateSMUParam());
}

public sealed class SmuRangePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateSmuRangeEditor(property, obj);
}

public sealed class SensorTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, FlowNodePropertyEditorRegistration.CreateSensorTemplate(obj));
}

public sealed class DataLoadTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateDataLoad());
}

public sealed class BlackMuraTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateBlackMura());
}

public sealed class ImageRoiTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateImageROI());
}

public sealed class PoiAnalysisTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplatePoiAnalysis());
}

public sealed class ImageCroppingTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateImageCropping());
}

public sealed class LedCheck2TemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateLedCheck2());
}

public sealed class KbTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj) => FlowNodePropertyEditorRegistration.CreateTemplateEditor(property, obj, new TemplateKB());
}
