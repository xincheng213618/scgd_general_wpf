using ColorVision.Engine.Services.PhyCameras.Calibration;
using ColorVision.Engine.Services.Types;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Group
{
    internal static class CalibrationArchivePlanBuilder
    {
        public static CalibrationExportPlan Create(PhyCamera phyCamera)
        {
            List<CalibrationExportFile> files = new();
            List<CalibrationExportText> textEntries = new();
            Dictionary<string, List<ZipCalibrationItem>> index = new();
            List<ZipCalibrationItem> calibrationItems = new();
            index.Add("Calibration", calibrationItems);

            foreach (object item in phyCamera.VisualChildren)
            {
                if (item is CalibrationResource calibrationResource)
                {
                    calibrationItems.Add(CreateItem(calibrationResource));

                    ServiceTypes serviceType = (ServiceTypes)calibrationResource.SysResourceModel.Type;
                    string relativePath = calibrationResource.SysResourceModel.Value ?? string.Empty;
                    string sourcePath = Path.Combine(
                        phyCamera.Config.FileServerCfg.FileBasePath,
                        phyCamera.Code,
                        "cfg",
                        relativePath);
                    if (File.Exists(sourcePath))
                    {
                        files.Add(new CalibrationExportFile(
                            sourcePath,
                            Path.Combine("Calibration", serviceType.ToString(), calibrationResource.Config.FileName)));
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

            return new CalibrationExportPlan(files, textEntries);
        }

        private static ZipCalibrationItem CreateItem(CalibrationResource calibrationResource) => new()
        {
            CalibrationType = ((ServiceTypes)calibrationResource.SysResourceModel.Type).ToCalibrationType(),
            Title = calibrationResource.Config.Title,
            FileName = calibrationResource.Config.FileName
        };
    }
}
