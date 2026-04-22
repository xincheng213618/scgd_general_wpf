using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    /// <summary>
    /// 3D 视口公共辅助方法 — 截图、相机重置、视口初始化
    /// </summary>
    public static class Viewport3DHelper
    {
        /// <summary>
        /// 创建带默认相机和鼠标手势的 HelixViewport3D
        /// </summary>
        public static HelixViewport3D CreateDefaultViewport(
            Point3D position,
            Vector3D lookDirection,
            Vector3D upDirection,
            double fieldOfView = 60)
        {
            return new HelixViewport3D
            {
                Camera = new PerspectiveCamera
                {
                    Position = position,
                    LookDirection = lookDirection,
                    UpDirection = upDirection,
                    FieldOfView = fieldOfView,
                },
                ShowFrameRate = false,
                ZoomExtentsWhenLoaded = true,
                IsRotationEnabled = true,
                IsMoveEnabled = true,
                IsPanEnabled = true,
                RotateGesture = new MouseGesture(MouseAction.LeftClick),
                PanGesture = new MouseGesture(MouseAction.RightClick),
            };
        }

        /// <summary>
        /// 重置相机到指定状态
        /// </summary>
        public static void ResetCameraView(
            PerspectiveCamera camera,
            Point3D position,
            Vector3D lookDirection,
            Vector3D upDirection)
        {
            if (camera == null) return;
            camera.Position = position;
            camera.LookDirection = lookDirection;
            camera.UpDirection = upDirection;
        }

        public static Rect3D GetBounds(Model3D model)
        {
            if (model is Model3DGroup group)
            {
                Rect3D bounds = Rect3D.Empty;
                foreach (var child in group.Children)
                {
                    var childBounds = GetBounds(child);
                    if (!childBounds.IsEmpty)
                    {
                        if (bounds.IsEmpty)
                            bounds = childBounds;
                        else
                            bounds.Union(childBounds);
                    }
                }
                return bounds;
            }

            if (model is GeometryModel3D geometry && geometry.Geometry is MeshGeometry3D mesh)
            {
                if (mesh.Positions.Count == 0)
                    return Rect3D.Empty;

                double minX = mesh.Positions[0].X, maxX = mesh.Positions[0].X;
                double minY = mesh.Positions[0].Y, maxY = mesh.Positions[0].Y;
                double minZ = mesh.Positions[0].Z, maxZ = mesh.Positions[0].Z;

                foreach (var p in mesh.Positions)
                {
                    minX = Math.Min(minX, p.X);
                    maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y);
                    maxY = Math.Max(maxY, p.Y);
                    minZ = Math.Min(minZ, p.Z);
                    maxZ = Math.Max(maxZ, p.Z);
                }

                return new Rect3D(minX, minY, minZ, maxX - minX, maxY - minY, maxZ - minZ);
            }

            return Rect3D.Empty;
        }

        public static void FrameModel(PerspectiveCamera camera, Rect3D bounds)
        {
            if (camera == null || bounds.IsEmpty) return;

            var center = new Point3D(
                bounds.X + bounds.SizeX / 2,
                bounds.Y + bounds.SizeY / 2,
                bounds.Z + bounds.SizeZ / 2);

            double radius = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
            if (radius <= 0) radius = 1;

            double distance = radius * 3.2;
            camera.Position = new Point3D(
                center.X + distance * 0.42,
                center.Y - distance,
                center.Z + distance * 0.78);
            camera.LookDirection = center - camera.Position;
            camera.UpDirection = new Vector3D(0, 0, 1);
            camera.NearPlaneDistance = Math.Max(distance / 500, 0.1);
            camera.FarPlaneDistance = Math.Max(distance * 120, 10000);
        }

        public static List<Visual3D> CreateFixedCornerAxes(double size)
        {
            double shaft = Math.Max(size * 0.08, 2.2);
            double sphere = Math.Max(size * 0.18, 3);

            return
            [
                new PipeVisual3D
                {
                    Point1 = new Point3D(0, 0, 0),
                    Point2 = new Point3D(size, 0, 0),
                    Diameter = shaft,
                    Fill = Brushes.Red
                },
                new PipeVisual3D
                {
                    Point1 = new Point3D(0, 0, 0),
                    Point2 = new Point3D(0, size, 0),
                    Diameter = shaft,
                    Fill = Brushes.LimeGreen
                },
                new PipeVisual3D
                {
                    Point1 = new Point3D(0, 0, 0),
                    Point2 = new Point3D(0, 0, size),
                    Diameter = shaft,
                    Fill = Brushes.DodgerBlue
                },
                new SphereVisual3D { Center = new Point3D(size, 0, 0), Radius = sphere, Fill = Brushes.Red },
                new SphereVisual3D { Center = new Point3D(0, size, 0), Radius = sphere, Fill = Brushes.LimeGreen },
                new SphereVisual3D { Center = new Point3D(0, 0, size), Radius = sphere, Fill = Brushes.DodgerBlue },
                new BillboardTextVisual3D { Position = new Point3D(size * 1.4, 0, 0), Text = "X", Foreground = Brushes.White, Background = Brushes.Black },
                new BillboardTextVisual3D { Position = new Point3D(0, size * 1.4, 0), Text = "Y", Foreground = Brushes.White, Background = Brushes.Black },
                new BillboardTextVisual3D { Position = new Point3D(0, 0, size * 1.4), Text = "Z", Foreground = Brushes.White, Background = Brushes.Black }
            ];
        }

        public static void UpdateFixedCornerAxes(IReadOnlyList<Visual3D> axes, PerspectiveCamera camera)
        {
            if (camera == null || axes.Count < 9) return;

            Vector3D forward = camera.LookDirection;
            if (forward.LengthSquared < 1e-6) return;
            forward.Normalize();

            Vector3D screenUp = camera.UpDirection;
            if (screenUp.LengthSquared < 1e-6)
                screenUp = new Vector3D(0, 0, 1);
            screenUp.Normalize();

            Vector3D screenRight = Vector3D.CrossProduct(forward, screenUp);
            if (screenRight.LengthSquared < 1e-6) return;
            screenRight.Normalize();
            screenUp = Vector3D.CrossProduct(screenRight, forward);
            if (screenUp.LengthSquared < 1e-6) return;
            screenUp.Normalize();

            Vector3D worldX = new Vector3D(1, 0, 0);
            Vector3D worldY = new Vector3D(0, 1, 0);
            Vector3D worldZ = new Vector3D(0, 0, 1);

            double cameraDistance = Math.Max(camera.LookDirection.Length, 1);
            double overlayDistance = Math.Max(cameraDistance * 0.18, 60);
            double axisSize = Math.Max(cameraDistance * 0.045, 18);
            double sphereRadius = Math.Max(axisSize * 0.16, 3);

            Point3D target = camera.Position + forward * overlayDistance;
            Point3D origin = target + screenRight * (overlayDistance * 0.72) - screenUp * (overlayDistance * 0.72);

            SetPipe((PipeVisual3D)axes[0], origin, worldX, axisSize);
            SetPipe((PipeVisual3D)axes[1], origin, worldY, axisSize);
            SetPipe((PipeVisual3D)axes[2], origin, worldZ, axisSize);

            SetSphere((SphereVisual3D)axes[3], origin, worldX, axisSize, sphereRadius);
            SetSphere((SphereVisual3D)axes[4], origin, worldY, axisSize, sphereRadius);
            SetSphere((SphereVisual3D)axes[5], origin, worldZ, axisSize, sphereRadius);

            SetLabel((BillboardTextVisual3D)axes[6], origin, worldX, axisSize, "X");
            SetLabel((BillboardTextVisual3D)axes[7], origin, worldY, axisSize, "Y");
            SetLabel((BillboardTextVisual3D)axes[8], origin, worldZ, axisSize, "Z");
        }

        private static void SetPipe(PipeVisual3D pipe, Point3D origin, Vector3D direction, double size)
        {
            direction.Normalize();
            pipe.Point1 = origin;
            pipe.Point2 = origin + direction * size;
            pipe.Diameter = Math.Max(size * 0.08, 2.2);
        }

        private static void SetSphere(SphereVisual3D sphere, Point3D origin, Vector3D direction, double size, double radius)
        {
            direction.Normalize();
            sphere.Center = origin + direction * size;
            sphere.Radius = radius;
        }

        private static void SetLabel(BillboardTextVisual3D label, Point3D origin, Vector3D direction, double size, string text)
        {
            direction.Normalize();
            label.Position = origin + direction * (size * 1.4);
            label.Text = text;
            label.Foreground = Brushes.White;
            label.Background = Brushes.Black;
        }

        public static void ApplyDefaultMaterial(Model3D model)
        {
            var material = MaterialHelper.CreateMaterial(Brushes.LightGray);
            ApplyMaterialRecursive(model, material);
        }

        private static void ApplyMaterialRecursive(Model3D model, Material material)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    ApplyMaterialRecursive(child, material);
                return;
            }

            if (model is GeometryModel3D geometry)
            {
                if (geometry.Material == null)
                    geometry.Material = material;
                if (geometry.BackMaterial == null)
                    geometry.BackMaterial = geometry.Material;
            }
        }

        public static void AddCameraAlignedLights(HelixViewport3D viewport, Rect3D bounds)
        {
            if (viewport == null) return;
            double size = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
            if (size <= 0) size = 1000;

            viewport.Children.Add(new SunLight());
            viewport.Children.Add(new DefaultLights());
        }

        public static void ClearLights(HelixViewport3D viewport)
        {
            if (viewport == null) return;
            for (int i = viewport.Children.Count - 1; i >= 0; i--)
            {
                if (viewport.Children[i] is LightVisual3D || viewport.Children[i] is DefaultLights || viewport.Children[i] is SunLight)
                    viewport.Children.RemoveAt(i);
            }
        }

        public static void ClearAxes(HelixViewport3D viewport)
        {
            if (viewport == null) return;
            for (int i = viewport.Children.Count - 1; i >= 0; i--)
            {
                if (viewport.Children[i] is ArrowVisual3D)
                    viewport.Children.RemoveAt(i);
            }
        }

        public static bool HasGeometry(Model3D model)
        {
            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (HasGeometry(child)) return true;
                }
                return false;
            }

            return model is GeometryModel3D geometry && geometry.Geometry is MeshGeometry3D mesh && mesh.Positions.Count > 0;
        }

        public static void TryZoomExtents(HelixViewport3D viewport)
        {
            try
            {
                viewport.ZoomExtents();
            }
            catch
            {
            }
        }

        /// <summary>
        /// 保存视口截图
        /// </summary>
        public static void SaveScreenshot(HelixViewport3D viewport, string defaultFileName)
        {
            if (viewport == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp",
                DefaultExt = "png",
                FileName = defaultFileName
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var renderBitmap = new RenderTargetBitmap(
                        (int)viewport.ActualWidth,
                        (int)viewport.ActualHeight,
                        96, 96,
                        PixelFormats.Pbgra32);

                    renderBitmap.Render(viewport);

                    BitmapEncoder encoder = dialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        ? new JpegBitmapEncoder()
                        : dialog.FileName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
                            ? new BmpBitmapEncoder()
                            : new PngBitmapEncoder();

                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                    using var stream = File.Create(dialog.FileName);
                    encoder.Save(stream);

                    MessageBox.Show($"Screenshot saved:\n{dialog.FileName}", "Screenshot Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save screenshot:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static void ExportModel(Model3D model, string defaultFileName)
        {
            if (model == null || !HasGeometry(model)) return;

            var dialog = new SaveFileDialog
            {
                Filter = "OBJ Model|*.obj|STL Model|*.stl",
                DefaultExt = "obj",
                FileName = defaultFileName
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (extension == ".stl")
                    WriteStl(model, dialog.FileName);
                else
                    WriteObj(model, dialog.FileName);

                MessageBox.Show($"Model exported:\n{dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export model:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ExportMesh(MeshGeometry3D mesh, string defaultFileName)
        {
            if (mesh == null || mesh.Positions.Count == 0 || mesh.TriangleIndices.Count < 3) return;

            var model = new GeometryModel3D { Geometry = mesh };
            ExportModel(model, defaultFileName);
        }

        private static void WriteObj(Model3D model, string filePath)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Exported by ColorVision");

            int vertexOffset = 1;
            int texcoordOffset = 1;
            int meshIndex = 0;
            WriteObjRecursive(model, Transform3D.Identity.Value, builder, ref vertexOffset, ref texcoordOffset, ref meshIndex);

            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        }

        private static void WriteObjRecursive(Model3D model, Matrix3D parentTransform, StringBuilder builder, ref int vertexOffset, ref int texcoordOffset, ref int meshIndex)
        {
            if (model == null) return;

            Matrix3D transform = parentTransform;
            if (model.Transform != null && !model.Transform.Value.IsIdentity)
                transform.Append(model.Transform.Value);

            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    WriteObjRecursive(child, transform, builder, ref vertexOffset, ref texcoordOffset, ref meshIndex);
                return;
            }

            if (model is not GeometryModel3D geometry || geometry.Geometry is not MeshGeometry3D mesh)
                return;
            if (mesh.Positions.Count == 0 || mesh.TriangleIndices.Count < 3)
                return;

            meshIndex++;
            builder.AppendLine($"o mesh_{meshIndex}");

            foreach (var position in mesh.Positions)
            {
                Point3D p = transform.Transform(position);
                builder.AppendLine(FormattableString.Invariant($"v {p.X} {p.Y} {p.Z}"));
            }

            bool hasTexcoords = mesh.TextureCoordinates != null && mesh.TextureCoordinates.Count == mesh.Positions.Count;
            if (hasTexcoords)
            {
                foreach (var uv in mesh.TextureCoordinates)
                    builder.AppendLine(FormattableString.Invariant($"vt {uv.X} {1 - uv.Y}"));
            }

            for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
            {
                int a = mesh.TriangleIndices[i] + vertexOffset;
                int b = mesh.TriangleIndices[i + 1] + vertexOffset;
                int c = mesh.TriangleIndices[i + 2] + vertexOffset;

                if (hasTexcoords)
                {
                    int ta = mesh.TriangleIndices[i] + texcoordOffset;
                    int tb = mesh.TriangleIndices[i + 1] + texcoordOffset;
                    int tc = mesh.TriangleIndices[i + 2] + texcoordOffset;
                    builder.AppendLine($"f {a}/{ta} {b}/{tb} {c}/{tc}");
                }
                else
                {
                    builder.AppendLine($"f {a} {b} {c}");
                }
            }

            vertexOffset += mesh.Positions.Count;
            if (hasTexcoords)
                texcoordOffset += mesh.TextureCoordinates.Count;
        }

        private static void WriteStl(Model3D model, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
            writer.WriteLine("solid ColorVision");
            WriteStlRecursive(model, Transform3D.Identity.Value, writer);
            writer.WriteLine("endsolid ColorVision");
        }

        private static void WriteStlRecursive(Model3D model, Matrix3D parentTransform, StreamWriter writer)
        {
            if (model == null) return;

            Matrix3D transform = parentTransform;
            if (model.Transform != null && !model.Transform.Value.IsIdentity)
                transform.Append(model.Transform.Value);

            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    WriteStlRecursive(child, transform, writer);
                return;
            }

            if (model is not GeometryModel3D geometry || geometry.Geometry is not MeshGeometry3D mesh)
                return;
            if (mesh.Positions.Count == 0 || mesh.TriangleIndices.Count < 3)
                return;

            for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
            {
                Point3D p1 = transform.Transform(mesh.Positions[mesh.TriangleIndices[i]]);
                Point3D p2 = transform.Transform(mesh.Positions[mesh.TriangleIndices[i + 1]]);
                Point3D p3 = transform.Transform(mesh.Positions[mesh.TriangleIndices[i + 2]]);
                Vector3D normal = CalculateNormal(p1, p2, p3);

                writer.WriteLine(FormattableString.Invariant($"  facet normal {normal.X} {normal.Y} {normal.Z}"));
                writer.WriteLine("    outer loop");
                writer.WriteLine(FormattableString.Invariant($"      vertex {p1.X} {p1.Y} {p1.Z}"));
                writer.WriteLine(FormattableString.Invariant($"      vertex {p2.X} {p2.Y} {p2.Z}"));
                writer.WriteLine(FormattableString.Invariant($"      vertex {p3.X} {p3.Y} {p3.Z}"));
                writer.WriteLine("    endloop");
                writer.WriteLine("  endfacet");
            }
        }

        private static Vector3D CalculateNormal(Point3D p1, Point3D p2, Point3D p3)
        {
            Vector3D u = p2 - p1;
            Vector3D v = p3 - p1;
            Vector3D normal = Vector3D.CrossProduct(u, v);
            if (normal.LengthSquared > 1e-12)
                normal.Normalize();
            else
                normal = new Vector3D(0, 0, 1);
            return normal;
        }
    }
}
