using ColorVision.Engine.Services.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    internal static class CalibrationArchivePlanBuilder
    {
        public static CalibrationExportPlan Create(PhyCamera phyCamera)
        {
            Dictionary<string, CalibrationExportFile> files = new(StringComparer.OrdinalIgnoreCase);
            List<CalibrationExportText> textEntries = new();
            Dictionary<string, List<ZipCalibrationItem>> index = new();
            List<ZipCalibrationItem> calibrationItems = new();
            index.Add("Calibration", calibrationItems);

            foreach (object item in phyCamera.VisualChildren)
            {
                if (item is CalibrationResource calibrationResource)
                {
                    ZipCalibrationItem calibrationItem = CreateItem(calibrationResource);
                    calibrationItems.Add(calibrationItem);

                    ServiceTypes serviceType = (ServiceTypes)calibrationResource.SysResourceModel.Type;
                    string relativePath = calibrationResource.SysResourceModel.Value ?? string.Empty;
                    string sourcePath = Path.Combine(
                        phyCamera.Config.FileServerCfg.FileBasePath,
                        phyCamera.Code,
                        "cfg",
                        relativePath);
                    if (File.Exists(sourcePath))
                    {
                        string entryPath = Path.Combine("Calibration", serviceType.ToString(), calibrationItem.FileName).Replace('\\', '/');
                        files[entryPath] = new CalibrationExportFile(sourcePath, entryPath);
                    }
                }

                if (item is GroupResource groupResource)
                {
                    List<ZipCalibrationItem> groupItems = new();
                    foreach (CalibrationResource calibration in groupResource.VisualChildren.OfType<CalibrationResource>())
                    {
                        groupItems.Add(CreateItem(calibration));
                    }

                    textEntries.Add(new CalibrationExportText(
                        Path.Combine("Calibration", $"{groupResource.Name}.cfg"),
                        JsonConvert.SerializeObject(groupItems, Formatting.Indented)));
                }
            }

            textEntries.Add(new CalibrationExportText(
                "Calibration.cfg",
                JsonConvert.SerializeObject(index, Formatting.Indented)));
            textEntries.Add(new CalibrationExportText(
                "Camera.cfg",
                JsonConvert.SerializeObject(phyCamera.Config, Formatting.Indented)));

            if (phyCamera.CameraLicenseModel != null)
            {
                textEntries.Add(new CalibrationExportText(
                    $"{phyCamera.Code}.lic",
                    phyCamera.CameraLicenseModel.LicenseValue ?? string.Empty));
            }

            return new CalibrationExportPlan(files.Values.ToArray(), textEntries);
        }

        private static ZipCalibrationItem CreateItem(CalibrationResource calibrationResource)
        {
            string? storedName = calibrationResource.SysResourceModel.Value;
            string fileName = !string.IsNullOrWhiteSpace(storedName) && storedName == Path.GetFileName(storedName)
                ? storedName
                : calibrationResource.Config.FileName;
            return new ZipCalibrationItem
            {
                CalibrationType = ((ServiceTypes)calibrationResource.SysResourceModel.Type).ToCalibrationType(),
                Title = calibrationResource.Config.Title,
                FileName = fileName
            };
        }
    }
}
