using ColorVision.Database;
using ColorVision.Engine.Templates;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    internal readonly record struct CalibrationTemplateSlotMapping(string FilePath, int Id, bool NeedsConfiguration);

    internal sealed record CalibrationTemplateCloneResult(
        int ClonedCount,
        IReadOnlyList<string> SkippedNames,
        IReadOnlyList<string> NeedsConfigurationNames);

    /// <summary>
    /// Copies camera-scoped calibration templates while preventing references to
    /// calibration files that belong to another physical camera.
    /// </summary>
    internal static class CalibrationTemplateCloneService
    {
        private const int CalibrationTemplateDictionaryId = 2;

        public static CalibrationTemplateCloneResult Clone(
            PhyCamera sourceCamera,
            PhyCamera targetCamera,
            IReadOnlyCollection<int> sourceTemplateIds)
        {
            ArgumentNullException.ThrowIfNull(sourceCamera);
            ArgumentNullException.ThrowIfNull(targetCamera);
            ArgumentNullException.ThrowIfNull(sourceTemplateIds);

            if (sourceCamera.Id == targetCamera.Id)
                throw new ArgumentException("The source and target physical cameras must be different.");

            HashSet<int> requestedIds = sourceTemplateIds.Where(id => id > 0).ToHashSet();
            if (requestedIds.Count == 0)
                return new CalibrationTemplateCloneResult(0, Array.Empty<string>(), Array.Empty<string>());
            List<int> requestedOrder = sourceTemplateIds.ToList();

            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });

            List<ModMasterModel> sourceMasters = db.Queryable<ModMasterModel>()
                .Where(model => model.Pid == CalibrationTemplateDictionaryId
                    && model.ResourceId == sourceCamera.Id
                    && model.TenantId == 0
                    && model.IsDelete == false)
                .ToList()
                .Where(model => requestedIds.Contains(model.Id))
                .OrderBy(model => requestedOrder.IndexOf(model.Id))
                .ToList();

            HashSet<string> targetNames = db.Queryable<ModMasterModel>()
                .Where(model => model.Pid == CalibrationTemplateDictionaryId
                    && model.ResourceId == targetCamera.Id
                    && model.TenantId == 0
                    && model.IsDelete == false)
                .Select(model => model.Name)
                .ToList()
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Dictionary<int, string> symbols = db.Queryable<SysDictionaryModDetaiModel>()
                .Where(model => model.PId == CalibrationTemplateDictionaryId && model.IsDelete == false)
                .ToList()
                .Where(model => !string.IsNullOrWhiteSpace(model.Symbol))
                .ToDictionary(model => model.Id, model => model.Symbol!);

            Dictionary<string, GroupResource> targetGroups = targetCamera.VisualChildren
                .OfType<GroupResource>()
                .Where(group => !string.IsNullOrWhiteSpace(group.Name))
                .GroupBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            List<string> skippedNames = new();
            List<string> needsConfigurationNames = new();
            int clonedCount = 0;

            db.Ado.BeginTran();
            try
            {
                foreach (ModMasterModel sourceMaster in sourceMasters)
                {
                    string templateName = sourceMaster.Name ?? sourceMaster.Id.ToString(CultureInfo.InvariantCulture);
                    if (!targetNames.Add(templateName))
                    {
                        skippedNames.Add(templateName);
                        continue;
                    }

                    ModMasterModel targetMaster = new()
                    {
                        Code = sourceMaster.Code,
                        Name = templateName,
                        Pid = CalibrationTemplateDictionaryId,
                        ResourceId = targetCamera.Id,
                        JsonVal = sourceMaster.JsonVal,
                        CreateDate = DateTime.Now,
                        IsEnable = sourceMaster.IsEnable,
                        IsDelete = false,
                        Remark = sourceMaster.Remark,
                        TenantId = 0
                    };

                    targetMaster.Id = db.Insertable(targetMaster).ExecuteReturnIdentity();
                    if (targetMaster.Id <= 0)
                        throw new InvalidOperationException($"Failed to create calibration template '{templateName}'.");

                    List<ModDetailModel> clonedDetails = db.Queryable<ModDetailModel>()
                        .Where(detail => detail.Pid == sourceMaster.Id)
                        .ToList()
                        .Select(detail => new ModDetailModel
                        {
                            SysPid = detail.SysPid,
                            Pid = targetMaster.Id,
                            ValueA = detail.ValueA,
                            ValueB = detail.ValueB,
                            IsEnable = detail.IsEnable,
                            IsDelete = detail.IsDelete
                        })
                        .ToList();

                    if (clonedDetails.Count == 0)
                        throw new InvalidOperationException($"Calibration template '{templateName}' has no parameter details.");

                    Dictionary<string, ModDetailModel> detailsBySymbol = clonedDetails
                        .Where(detail => symbols.ContainsKey(detail.SysPid))
                        .GroupBy(detail => symbols[detail.SysPid], StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

                    string calibrationMode = GetValue(detailsBySymbol, nameof(CalibrationParam.CalibrationMode));
                    targetGroups.TryGetValue(calibrationMode, out GroupResource? targetGroup);
                    targetGroup?.SetCalibrationResource();
                    if (targetGroup != null)
                        SetValue(detailsBySymbol, nameof(CalibrationParam.CalibrationMode), targetGroup.Name);

                    bool needsConfiguration = false;
                    bool hasSelectedCalibration = false;
                    foreach (CalibrationSlotDefinition slot in CalibrationSlotDefinitions.AllSlots)
                    {
                        bool isSelected = bool.TryParse(GetValue(detailsBySymbol, slot.Key + "IsSelected"), out bool selected) && selected;
                        hasSelectedCalibration |= isSelected;
                        CalibrationResource? targetResource = targetGroup == null ? null : slot.GroupGetter(targetGroup);
                        CalibrationTemplateSlotMapping mapping = MapSlot(
                            isSelected,
                            targetResource?.Name,
                            targetResource?.Id ?? 0,
                            targetResource?.IsValid == true);

                        SetValue(detailsBySymbol, slot.Key, mapping.FilePath);
                        SetValue(detailsBySymbol, slot.Key + "Id", mapping.Id.ToString(CultureInfo.InvariantCulture));
                        needsConfiguration |= mapping.NeedsConfiguration;
                    }
                    needsConfiguration |= targetGroup == null || !hasSelectedCalibration;

                    db.Insertable(clonedDetails).ExecuteCommand();
                    if (needsConfiguration)
                        needsConfigurationNames.Add(templateName);

                    clonedCount++;
                }

                db.Ado.CommitTran();
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }

            CalibrationParam.LoadResourceParams(targetCamera.CalibrationParams, targetCamera.Id);
            return new CalibrationTemplateCloneResult(clonedCount, skippedNames, needsConfigurationNames);
        }

        internal static CalibrationTemplateSlotMapping MapSlot(
            bool isSelected,
            string? targetResourceName,
            int targetResourceId,
            bool targetResourceIsValid = true)
        {
            bool hasTargetResource = !string.IsNullOrWhiteSpace(targetResourceName) && targetResourceId > 0;
            return hasTargetResource
                ? new CalibrationTemplateSlotMapping(targetResourceName!, targetResourceId, isSelected && !targetResourceIsValid)
                : new CalibrationTemplateSlotMapping(string.Empty, 0, isSelected);
        }

        private static string GetValue(
            Dictionary<string, ModDetailModel> detailsBySymbol,
            string symbol)
        {
            return detailsBySymbol.TryGetValue(symbol, out ModDetailModel? detail)
                ? detail.ValueA ?? string.Empty
                : string.Empty;
        }

        private static void SetValue(
            Dictionary<string, ModDetailModel> detailsBySymbol,
            string symbol,
            string value)
        {
            if (detailsBySymbol.TryGetValue(symbol, out ModDetailModel? detail))
            {
                detail.ValueB = detail.ValueA;
                detail.ValueA = value;
            }
        }
    }
}
