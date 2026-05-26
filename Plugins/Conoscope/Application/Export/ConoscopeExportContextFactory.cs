using ColorVision.UI;
using Conoscope.Core;
using System;
using System.Collections.Generic;

namespace Conoscope.ApplicationServices.Export
{
    public static class ConoscopeExportContextFactory
    {
        public static ConoscopeExportContext Create(
            ConoscopeModelProfile modelProfile, string modelName,
            int imageWidth, int imageHeight,
            System.Windows.Point center, double maxAngle, double currentPixelsPerDegree,
            Func<int, int, ConoscopeXyzValue> readXyz,
            Func<int, int, double>? readColorDifference,
            Func<int, int, double>? readContrast)
        {
            double pixelsPerDegree = currentPixelsPerDegree > 0
                ? currentPixelsPerDegree
                : modelProfile.GetConoscopeCoefficient(imageWidth, imageHeight);

            return new ConoscopeExportContext
            {
                ModelName = modelName,
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                Center = center,
                MaxAngle = maxAngle,
                PixelsPerDegree = pixelsPerDegree,
                ReadXyz = readXyz,
                ReadColorDifference = readColorDifference,
                ReadContrast = readContrast
            };
        }

        public static int GetDecimalPlaces()
        {
            return ConoscopeManager.GetInstance().Config.Export.DecimalPlaces;
        }

        public static AdvancedExportSettings GetAdvancedExportSettings()
        {
            ConoscopeConfig config = ConoscopeManager.GetInstance().Config;
            ConoscopeAdvancedExportState saved = config.AdvancedExport;

            return new AdvancedExportSettings
            {
                FilePrefix = saved.FilePrefix,
                Channels = saved.Channels is { Count: > 0 }
                    ? new List<ExportChannel>(saved.Channels)
                    : new List<ExportChannel> { ExportChannel.Y },
                ExportAzimuth = saved.ExportAzimuth,
                ExportPolar = saved.ExportPolar,
                AzimuthStep = saved.AzimuthStep,
                RadialStep = saved.RadialStep,
                PolarStep = saved.PolarStep,
                CircumferentialStep = saved.CircumferentialStep,
                DecimalPlaces = config.Export.DecimalPlaces,
                EnableCrossSection = saved.EnableCrossSection,
                CrossSectionType = saved.UseAzimuthCrossSection ? CrossSectionType.Azimuth : CrossSectionType.Polar,
                CrossSectionAzimuthAngle = saved.CrossSectionAzimuthAngle,
                CrossSectionPolarAngle = saved.CrossSectionPolarAngle,
                CrossSectionAngle = saved.UseAzimuthCrossSection ? saved.CrossSectionAzimuthAngle : saved.CrossSectionPolarAngle
            };
        }

        public static void SaveAdvancedExportSettings(AdvancedExportSettings settings)
        {
            ConoscopeConfig config = ConoscopeManager.GetInstance().Config;
            config.AdvancedExport = new ConoscopeAdvancedExportState
            {
                FilePrefix = settings.FilePrefix,
                Channels = settings.Channels.Count > 0
                    ? new List<ExportChannel>(settings.Channels)
                    : new List<ExportChannel> { ExportChannel.Y },
                ExportAzimuth = settings.ExportAzimuth,
                ExportPolar = settings.ExportPolar,
                AzimuthStep = settings.AzimuthStep,
                RadialStep = settings.RadialStep,
                PolarStep = settings.PolarStep,
                CircumferentialStep = settings.CircumferentialStep,
                EnableCrossSection = settings.EnableCrossSection,
                UseAzimuthCrossSection = settings.CrossSectionType == CrossSectionType.Azimuth,
                CrossSectionAzimuthAngle = settings.CrossSectionAzimuthAngle,
                CrossSectionPolarAngle = settings.CrossSectionPolarAngle
            };
            config.Export.DecimalPlaces = settings.DecimalPlaces;

            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
            }
            catch (Exception)
            {
            }
        }

        public static ConoscopeCrossSectionExportOptions GetCurrentCurveExportOptions()
        {
            ConoscopeExportSettings exportConfig = ConoscopeManager.GetInstance().Config.Export;
            return new ConoscopeCrossSectionExportOptions
            {
                StepDegrees = exportConfig.CurrentCurveStepDegrees,
                IncludeMetadata = exportConfig.IncludeMetadata,
                DecimalPlaces = exportConfig.DecimalPlaces
            };
        }

        public static void SaveCurrentCurveExportOptions(ConoscopeCrossSectionExportOptions options)
        {
            ConoscopeExportSettings exportConfig = ConoscopeManager.GetInstance().Config.Export;
            exportConfig.CurrentCurveStepDegrees = options.StepDegrees;
            exportConfig.IncludeMetadata = options.IncludeMetadata;
            exportConfig.DecimalPlaces = options.DecimalPlaces;

            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
            }
            catch (Exception)
            {
            }
        }

        public static ConoscopeCrossSectionExportOptions CreateAdvancedCrossSectionExportOptions(AdvancedExportSettings settings)
        {
            return new ConoscopeCrossSectionExportOptions
            {
                StepDegrees = settings.CrossSectionType == CrossSectionType.Azimuth
                    ? settings.RadialStep
                    : settings.CircumferentialStep,
                IncludeMetadata = true,
                DecimalPlaces = settings.DecimalPlaces
            };
        }

        public static bool IsChannelReady(ExportChannel channel, bool hasXyzData, bool hasYMat, bool canRefreshContrast, bool canRefreshColorDifference)
        {
            if (channel == ExportChannel.Y)
            {
                return hasYMat;
            }

            if (!hasXyzData)
            {
                return false;
            }

            if (channel == ExportChannel.Contrast && !canRefreshContrast)
            {
                return false;
            }

            if (channel == ExportChannel.ColorDifference && !canRefreshColorDifference)
            {
                return false;
            }

            return true;
        }
    }
}
