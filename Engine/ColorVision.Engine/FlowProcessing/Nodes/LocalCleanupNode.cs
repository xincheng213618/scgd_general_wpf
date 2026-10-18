using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Files;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes;

public sealed class CleanupSettingsDescriptor : STNodePropertyDescriptor
{
    protected override string GetStringFromValue(bool isLang = false) => JsonConvert.SerializeObject(PropertyInfo.GetValue(Node));
    protected override object GetValueFromString(string strText) => JsonConvert.DeserializeObject(strText, PropertyInfo.PropertyType)
        ?? throw new FormatException("Invalid cleanup setting.");
}

[STNode("Flow_CustomNodes/Flow_LocalAlgorithms", "LocalCleanup_NodeDescription", CategoryOrder = 9900, NodeOrder = 110)]
public sealed class LocalCleanupNode : LocalFlowNodeBase
{
    private readonly FolderSizePreProcessorConfig config = new() { FolderPaths = [] };

    [Category("PreProcess_CacheCleanupGroup")]
    [PropertyEditorType(typeof(CollectionJsonEditor))]
    [CollectionEditorType(typeof(TextSelectFolderPropertiesEditor))]
    [STNodeProperty("PreProcess_CacheFolders", "PreProcess_CacheFoldersDesc", true, DescriptorType = typeof(CleanupSettingsDescriptor))]
    public string[] FolderPaths { get => config.FolderPaths.ToArray(); set { config.FolderPaths = (value ?? []).ToList(); OnPropertyChanged(); } }

    [Category("PreProcess_CacheCleanupGroup")]
    [PropertyEditorType(typeof(FolderSizeBytesPropertiesEditor))]
    [STNodeProperty("PreProcess_CacheLimit", "PreProcess_CacheLimitDesc", true, DescriptorType = typeof(CleanupSettingsDescriptor))]
    public long TriggerSizeBytes { get => config.TriggerSizeBytes; set { config.TriggerSizeBytes = value; OnPropertyChanged(); } }

    [Category("PreProcess_CacheCleanupGroup")]
    [PropertyEditorType(typeof(FolderSizeBytesPropertiesEditor))]
    [STNodeProperty("PreProcess_CleanupTarget", "PreProcess_CleanupTargetDesc", true, DescriptorType = typeof(CleanupSettingsDescriptor))]
    public long TargetSizeBytes { get => config.TargetSizeBytes; set { config.TargetSizeBytes = value; OnPropertyChanged(); } }

    [Category("PreProcess_CacheCleanupGroup")]
    [STNodeProperty("PreProcess_CacheFileTypes", "PreProcess_CacheFileTypesDesc", true)]
    public string FileExtensions { get => config.FileExtensions; set { config.FileExtensions = value ?? string.Empty; OnPropertyChanged(); } }

    [Category("PreProcess_CacheCleanupGroup")]
    [STNodeProperty("PreProcess_IncludeSubfolders", "PreProcess_IncludeSubfoldersDesc", true)]
    public bool IncludeSubfolders { get => config.IncludeSubfolders; set { config.IncludeSubfolders = value; OnPropertyChanged(); } }

    public LocalCleanupNode() : base(EngineLocalization.Get("LocalCleanup_NodeName"), "LocalCleanup", "Cleanup") { }

    protected override string GetCompactSummaryValue() => CompactValueOrDash(FolderPaths.FirstOrDefault());

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        FolderCleanupResult result = ExecuteSynchronously(action);
        return new LocalNodeExecutionResult
        {
            Data = result,
            Message = EngineLocalization.Get(result.FailedFiles > 0 ? "LocalCleanup_CompletedWithFailures" : "LocalCleanup_Completed")
        };
    }

    internal FolderCleanupResult ExecuteSynchronously(CVStartCFC action)
    {
        string[] folders = FolderPaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        if (folders.Length == 0) throw new InvalidOperationException(EngineLocalization.Get("LocalCleanup_FoldersRequired"));
        long trigger = TriggerSizeBytes, target = TargetSizeBytes;
        if (trigger <= 0 || target < 0 || target >= trigger)
            throw new InvalidOperationException(EngineLocalization.Get("LocalCleanup_InvalidThresholds"));

        var protectedFiles = new List<string>();
        if (action.TryGetCurrentFrame(out LocalFlowFrame? frame) && frame != null)
        {
            protectedFiles.Add(frame.CvRawFilePath);
            protectedFiles.Add(frame.CvCieFilePath);
        }
        if (action.Data.TryGetValue("MasterValue", out object? masterValue) && masterValue is string path && System.IO.File.Exists(path))
            protectedFiles.Add(path);

        FolderCleanupResult result = FolderCleanupService.Execute(folders, FileExtensions, IncludeSubfolders, trigger, target,
            protectedFiles, () =>
            {
                if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _))
                    throw new OperationCanceledException(EngineLocalization.Get("LocalCleanup_Stopped"));
            });
        action.Data["CleanupDeletedFiles"] = result.DeletedFiles;
        action.Data["CleanupDeletedBytes"] = result.DeletedBytes;
        action.Data["CleanupRemainingBytes"] = result.RemainingBytes;
        action.Data["CleanupFailedFiles"] = result.FailedFiles;
        return result;
    }

    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    {
        ServiceName = NodeName, EventName = OperatorCode, action.SerialNumber,
        FolderPaths, TriggerSizeBytes, TargetSizeBytes, FileExtensions, IncludeSubfolders
    });
}
