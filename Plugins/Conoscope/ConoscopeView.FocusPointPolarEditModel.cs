using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;
using System;
using System.ComponentModel;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        public sealed class FocusPointPolarEditModel : ViewModelBase
        {
            private ConoscopeView? owner;
            private DVCircleText? circle;
            private bool isUpdating;
            private string name = string.Empty;
            private double azimuthDegrees;
            private double polarDegrees;
            private double distancePixels;
            private double radiusPixels;
            private double radiusDegrees;

            public FocusPointPolarEditModel()
            {
            }

            public void Initialize(ConoscopeView owner, DVCircleText circle)
            {
                this.owner = owner;
                this.circle = circle;
                SyncFromCircle();
            }

            [Category("关注点"), DisplayName("名称")]
            public string Name
            {
                get => name;
                set
                {
                    string newValue = value ?? string.Empty;
                    if (name == newValue)
                    {
                        return;
                    }

                    name = newValue;
                    if (circle != null)
                    {
                        circle.Attribute.Text = name;
                        ApplyVisualUpdate();
                    }

                    OnPropertyChanged();
                }
            }

            [Category("位置"), DisplayName("方位角(°)")]
            public double AzimuthDegrees
            {
                get => azimuthDegrees;
                set
                {
                    double normalized = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(value);
                    if (AreClose(azimuthDegrees, normalized))
                    {
                        return;
                    }

                    azimuthDegrees = normalized;
                    OnPropertyChanged();
                    ApplyCenterFromPolar();
                }
            }

            [Category("位置"), DisplayName("极角(°)")]
            public double PolarDegrees
            {
                get => polarDegrees;
                set
                {
                    double clamped = owner == null ? Math.Max(0, value) : Math.Max(0, Math.Min(value, owner.MaxAngle));
                    if (AreClose(polarDegrees, clamped))
                    {
                        return;
                    }

                    polarDegrees = clamped;
                    distancePixels = owner != null ? FocusPointMeasurementService.GetPolarDistancePixels(polarDegrees, owner.currentPixelsPerDegree, owner.currentImageRadius, owner.MaxAngle) : distancePixels;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DistancePixels));
                    ApplyCenterFromPolar();
                }
            }

            [Category("位置"), DisplayName("距离(px)")]
            public double DistancePixels
            {
                get => distancePixels;
                set
                {
                    double clamped = ClampDistancePixels(value);
                    if (AreClose(distancePixels, clamped))
                    {
                        return;
                    }

                    distancePixels = clamped;
                    polarDegrees = owner != null ? FocusPointMeasurementService.GetPolarAngleFromDistancePixels(distancePixels, owner.currentPixelsPerDegree, owner.currentImageRadius, owner.MaxAngle) : polarDegrees;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PolarDegrees));
                    ApplyCenterFromPolar();
                }
            }

            [Category("大小"), DisplayName("半径(px)")]
            public double RadiusPixels
            {
                get => radiusPixels;
                set
                {
                    double clamped = Math.Max(value, ConoscopeImageHost.MinimumFocusCircleRadius);
                    if (AreClose(radiusPixels, clamped))
                    {
                        return;
                    }

                    radiusPixels = clamped;
                    radiusDegrees = owner != null ? FocusPointMeasurementService.GetFocusCircleRadiusAngle(radiusPixels, owner.currentPixelsPerDegree, owner.currentImageRadius, owner.MaxAngle) : radiusDegrees;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RadiusDegrees));
                    ApplyRadius();
                }
            }

            [Category("大小"), DisplayName("半径(°)")]
            public double RadiusDegrees
            {
                get => radiusDegrees;
                set
                {
                    double clamped = owner == null ? Math.Max(0, value) : Math.Max(0, Math.Min(value, owner.MaxAngle));
                    if (AreClose(radiusDegrees, clamped))
                    {
                        return;
                    }

                    radiusDegrees = clamped;
                    radiusPixels = owner != null ? FocusPointMeasurementService.GetFocusCircleRadiusPixelsFromAngle(radiusDegrees, owner.currentPixelsPerDegree, owner.currentImageRadius, owner.MaxAngle, ConoscopeImageHost.MinimumFocusCircleRadius) : radiusPixels;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RadiusPixels));
                    ApplyRadius();
                }
            }

            private void SyncFromCircle()
            {
                if (owner == null || circle == null)
                {
                    return;
                }

                isUpdating = true;
                try
                {
                    name = ResolveFocusCircleName(circle);
                    Point center = circle.Attribute.Center;
                    azimuthDegrees = FocusPointMeasurementService.GetFullAzimuthAngle(center, owner.currentImageCenter);
                    polarDegrees = FocusPointMeasurementService.GetPolarRadiusAngle(center, owner.currentImageCenter, owner.currentImageRadius, owner.MaxAngle);
                    distancePixels = (center - owner.currentImageCenter).Length;
                    radiusPixels = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
                    radiusDegrees = FocusPointMeasurementService.GetFocusCircleRadiusAngle(radiusPixels, owner.currentPixelsPerDegree, owner.currentImageRadius, owner.MaxAngle);
                }
                finally
                {
                    isUpdating = false;
                }

                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(AzimuthDegrees));
                OnPropertyChanged(nameof(PolarDegrees));
                OnPropertyChanged(nameof(DistancePixels));
                OnPropertyChanged(nameof(RadiusPixels));
                OnPropertyChanged(nameof(RadiusDegrees));
            }

            private void ApplyCenterFromPolar()
            {
                if (isUpdating || owner == null || circle == null)
                {
                    return;
                }

                circle.Attribute.Center = FocusPointMeasurementService.CreatePointFromPolar(azimuthDegrees, distancePixels, owner.currentImageCenter);
                ApplyVisualUpdate();
            }

            private void ApplyRadius()
            {
                if (isUpdating || owner == null || circle == null)
                {
                    return;
                }

                circle.Attribute.Radius = radiusPixels;
                circle.Attribute.RadiusY = radiusPixels;
                ApplyVisualUpdate();
            }

            private double ClampDistancePixels(double value)
            {
                double distance = Math.Max(0, value);
                if (owner?.currentImageRadius > 0)
                {
                    distance = Math.Min(distance, owner.currentImageRadius);
                }

                return distance;
            }

            private void ApplyVisualUpdate()
            {
                if (owner == null || circle == null)
                {
                    return;
                }

                circle.Render();
                owner.ImageView.RefreshFocusCircleSelection();
                owner.UpdateSelectedFocusPointInfo();
            }

            private static bool AreClose(double left, double right)
            {
                return Math.Abs(left - right) < 0.000001;
            }
        }
    }
}
