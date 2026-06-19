#pragma warning disable CS8602
using HelixToolkit.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
                IsRotationEnabled = false,
                IsMoveEnabled = true,
                IsPanEnabled = true,
                RotateGesture = null,
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

        public static void UpdateFixedCornerAxes(IReadOnlyList<Visual3D> axes, ProjectionCamera camera)
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
            var material = MaterialHelper.CreateMaterial(Brushes.White);
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

            Vector3D forward = new Vector3D(0.3, -1, -0.5);
            Vector3D up = new Vector3D(0, 0, 1);

            if (viewport.Camera is ProjectionCamera camera && camera.LookDirection.LengthSquared > 1e-12)
            {
                forward = camera.LookDirection;
                forward.Normalize();

                if (camera.UpDirection.LengthSquared > 1e-12)
                {
                    up = camera.UpDirection;
                    up.Normalize();
                }
            }

            Vector3D right = Vector3D.CrossProduct(forward, up);
            if (right.LengthSquared <= 1e-12)
                right = new Vector3D(1, 0, 0);
            else
                right.Normalize();

            up = Vector3D.CrossProduct(right, forward);
            if (up.LengthSquared <= 1e-12)
                up = new Vector3D(0, 0, 1);
            else
                up.Normalize();

            viewport.Children.Add(new ModelVisual3D { Content = new AmbientLight(Color.FromRgb(128, 128, 128)) });
            viewport.Children.Add(new ModelVisual3D { Content = new DirectionalLight(Colors.White, -forward) });
            viewport.Children.Add(new ModelVisual3D { Content = new DirectionalLight(Color.FromRgb(190, 190, 190), NormalizeLightDirection(-forward - right * 0.65 + up * 0.35)) });
            viewport.Children.Add(new ModelVisual3D { Content = new DirectionalLight(Color.FromRgb(120, 120, 120), NormalizeLightDirection(-forward + right * 0.45 - up * 0.2)) });
        }

        private static Vector3D NormalizeLightDirection(Vector3D direction)
        {
            if (direction.LengthSquared <= 1e-12)
                return new Vector3D(0, -1, -0.4);

            direction.Normalize();
            return direction;
        }

        public static void ClearLights(HelixViewport3D viewport)
        {
            if (viewport == null) return;
            for (int i = viewport.Children.Count - 1; i >= 0; i--)
            {
                if (viewport.Children[i] is LightVisual3D ||
                    viewport.Children[i] is DefaultLights ||
                    viewport.Children[i] is SunLight ||
                    viewport.Children[i] is ModelVisual3D modelVisual && modelVisual.Content is Light)
                    viewport.Children.RemoveAt(i);
            }
        }

        public static void ClearAxes(HelixViewport3D viewport)
        {
            if (viewport == null) return;
            for (int i = viewport.Children.Count - 1; i >= 0; i--)
            {
                var child = viewport.Children[i];
                if (child is PipeVisual3D || child is SphereVisual3D || child is BillboardTextVisual3D)
                    viewport.Children.RemoveAt(i);
            }
        }

        /// <summary>
        /// 创建线框几何体 — 将网格的边渲染为细圆柱体
        /// </summary>
        public static Model3DGroup? CreateWireframeGeometry(Model3DGroup modelGroup, double diameter = 0.5, int thetaDiv = 4)
        {
            var wireframeGroup = new Model3DGroup();
            CreateWireframeRecursive(modelGroup, Transform3D.Identity, wireframeGroup, diameter, thetaDiv);
            return wireframeGroup.Children.Count > 0 ? wireframeGroup : null;
        }

        private static void CreateWireframeRecursive(Model3D model, Transform3D parentTransform, Model3DGroup wireframeGroup, double diameter, int thetaDiv)
        {
            var transform = parentTransform;
            if (model.Transform != null && !model.Transform.Value.IsIdentity)
                transform = new MatrixTransform3D(parentTransform.Value * model.Transform.Value);

            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    CreateWireframeRecursive(child, transform, wireframeGroup, diameter, thetaDiv);
                return;
            }

            if (model is not GeometryModel3D geometry || geometry.Geometry is not MeshGeometry3D mesh)
                return;
            if (mesh.Positions.Count == 0 || mesh.TriangleIndices.Count < 3)
                return;

            var edgeIndices = MeshGeometryHelper.FindEdges(mesh);
            if (edgeIndices.Count < 2)
                return;

            var transformedPositions = new List<Point3D>(mesh.Positions.Count);
            foreach (var p in mesh.Positions)
                transformedPositions.Add(transform.Transform(p));

            var builder = new MeshBuilder(false, false);
            builder.AddEdges(transformedPositions, edgeIndices, diameter, thetaDiv);
            var edgeMesh = builder.ToMesh();
            edgeMesh.Freeze();

            var edgeMaterial = MaterialHelper.CreateMaterial(Brushes.LimeGreen);
            var edgeModel = new GeometryModel3D(edgeMesh, edgeMaterial) { BackMaterial = edgeMaterial };
            wireframeGroup.Children.Add(edgeModel);
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

        /// <summary>
        /// 处理相机键盘移动和视角方向按键，返回是否已处理
        /// </summary>
        public static bool HandleCameraKey(ProjectionCamera camera, Key key, double moveSpeed = 20.0, double lookSpeed = 10.0)
        {
            if (camera == null) return false;

            switch (key)
            {
                case Key.L:
                    camera.Position = new Point3D(camera.Position.X - moveSpeed, camera.Position.Y, camera.Position.Z);
                    return true;
                case Key.T:
                    camera.Position = new Point3D(camera.Position.X, camera.Position.Y + moveSpeed, camera.Position.Z);
                    return true;
                case Key.R:
                    camera.Position = new Point3D(camera.Position.X + moveSpeed, camera.Position.Y, camera.Position.Z);
                    return true;
                case Key.B:
                    camera.Position = new Point3D(camera.Position.X, camera.Position.Y - moveSpeed, camera.Position.Z);
                    return true;
                case Key.A:
                    camera.LookDirection = new Vector3D(camera.LookDirection.X, camera.LookDirection.Y, camera.LookDirection.Z + lookSpeed);
                    return true;
                case Key.C:
                    camera.LookDirection = new Vector3D(camera.LookDirection.X, camera.LookDirection.Y, camera.LookDirection.Z - lookSpeed);
                    return true;
                case Key.D:
                    camera.LookDirection = new Vector3D(camera.LookDirection.X - lookSpeed, camera.LookDirection.Y, camera.LookDirection.Z);
                    return true;
                case Key.F:
                    camera.LookDirection = new Vector3D(camera.LookDirection.X + lookSpeed, camera.LookDirection.Y, camera.LookDirection.Z);
                    return true;
                default:
                    return false;
            }
        }

        public static void ExportMesh(MeshGeometry3D mesh, string defaultFileName)
        {
            if (mesh == null || mesh.Positions.Count == 0 || mesh.TriangleIndices.Count < 3) return;

            var model = new GeometryModel3D { Geometry = mesh };
            ExportModel(model, defaultFileName);
        }

        private sealed class ObjMaterialExportState
        {
            public required string MaterialName { get; init; }
            public string? TextureFileName { get; init; }
        }

        private static void WriteObj(Model3D model, string filePath)
        {
            var objBuilder = new StringBuilder();
            var mtlBuilder = new StringBuilder();
            objBuilder.AppendLine("# Exported by ColorVision");

            string materialFileName = Path.GetFileNameWithoutExtension(filePath) + ".mtl";
            objBuilder.AppendLine($"mtllib {materialFileName}");
            objBuilder.AppendLine();

            int vertexOffset = 1;
            int texcoordOffset = 1;
            int meshIndex = 0;
            int materialIndex = 0;
            bool hasMaterials = false;
            WriteObjRecursive(model, Transform3D.Identity.Value, filePath, objBuilder, mtlBuilder, ref vertexOffset, ref texcoordOffset, ref meshIndex, ref materialIndex, ref hasMaterials);

            File.WriteAllText(filePath, objBuilder.ToString(), Encoding.UTF8);
            if (hasMaterials)
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, materialFileName), mtlBuilder.ToString(), Encoding.UTF8);
        }

        private static void WriteObjRecursive(Model3D model, Matrix3D parentTransform, string objFilePath, StringBuilder objBuilder, StringBuilder mtlBuilder, ref int vertexOffset, ref int texcoordOffset, ref int meshIndex, ref int materialIndex, ref bool hasMaterials)
        {
            if (model == null) return;

            Matrix3D transform = parentTransform;
            if (model.Transform != null && !model.Transform.Value.IsIdentity)
                transform.Append(model.Transform.Value);

            if (model is Model3DGroup group)
            {
                foreach (var child in group.Children)
                    WriteObjRecursive(child, transform, objFilePath, objBuilder, mtlBuilder, ref vertexOffset, ref texcoordOffset, ref meshIndex, ref materialIndex, ref hasMaterials);
                return;
            }

            if (model is not GeometryModel3D geometry || geometry.Geometry is not MeshGeometry3D mesh)
                return;
            if (mesh.Positions.Count == 0 || mesh.TriangleIndices.Count < 3)
                return;

            meshIndex++;
            objBuilder.AppendLine($"o mesh_{meshIndex}");

            ObjMaterialExportState? materialState = CreateObjMaterial(geometry.Material, objFilePath, mtlBuilder, ref materialIndex);
            if (materialState != null)
            {
                hasMaterials = true;
                objBuilder.AppendLine($"usemtl {materialState.MaterialName}");
            }

            foreach (var position in mesh.Positions)
            {
                Point3D p = transform.Transform(position);
                objBuilder.AppendLine(FormattableString.Invariant($"v {p.X} {p.Y} {p.Z}"));
            }

            bool hasTexcoords = mesh.TextureCoordinates != null && mesh.TextureCoordinates.Count == mesh.Positions.Count;
            if (hasTexcoords)
            {
                foreach (var uv in mesh.TextureCoordinates)
                    objBuilder.AppendLine(FormattableString.Invariant($"vt {uv.X} {1 - uv.Y}"));
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
                    objBuilder.AppendLine($"f {a}/{ta} {b}/{tb} {c}/{tc}");
                }
                else
                {
                    objBuilder.AppendLine($"f {a} {b} {c}");
                }
            }

            objBuilder.AppendLine();
            vertexOffset += mesh.Positions.Count;
            if (hasTexcoords)
                texcoordOffset += mesh.TextureCoordinates.Count;
        }

        private static ObjMaterialExportState? CreateObjMaterial(Material? material, string objFilePath, StringBuilder mtlBuilder, ref int materialIndex)
        {
            if (material == null)
                return null;

            if (material is MaterialGroup group)
            {
                foreach (var child in group.Children)
                {
                    var childState = CreateObjMaterial(child, objFilePath, mtlBuilder, ref materialIndex);
                    if (childState != null)
                        return childState;
                }
                return null;
            }

            if (material is not DiffuseMaterial diffuse)
                return null;

            materialIndex++;
            string materialName = $"mat_{materialIndex}";
            string? textureFileName = null;
            Color kd = Colors.White;

            if (diffuse.Brush is SolidColorBrush solidColorBrush)
            {
                kd = solidColorBrush.Color;
            }
            else if (diffuse.Brush is ImageBrush imageBrush)
            {
                textureFileName = ExportObjTexture(imageBrush, objFilePath, materialName);
            }

            mtlBuilder.AppendLine($"newmtl {materialName}");
            mtlBuilder.AppendLine(FormattableString.Invariant($"Kd {kd.R / 255.0:F6} {kd.G / 255.0:F6} {kd.B / 255.0:F6}"));
            mtlBuilder.AppendLine("Ka 0.000000 0.000000 0.000000");
            mtlBuilder.AppendLine("Ks 0.000000 0.000000 0.000000");
            mtlBuilder.AppendLine("d 1.000000");
            mtlBuilder.AppendLine("illum 1");
            if (!string.IsNullOrWhiteSpace(textureFileName))
                mtlBuilder.AppendLine($"map_Kd {textureFileName}");
            mtlBuilder.AppendLine();

            return new ObjMaterialExportState
            {
                MaterialName = materialName,
                TextureFileName = textureFileName
            };
        }

        private static string? ExportObjTexture(ImageBrush imageBrush, string objFilePath, string materialName)
        {
            BitmapSource? bitmapSource = ExtractBitmapSource(imageBrush);
            if (bitmapSource == null)
                return null;

            string directory = Path.GetDirectoryName(objFilePath) ?? string.Empty;
            string textureFileName = Path.GetFileNameWithoutExtension(objFilePath) + $"_{materialName}.png";
            string texturePath = Path.Combine(directory, textureFileName);

            BitmapSource sourceToSave = bitmapSource.Format == PixelFormats.Bgra32 || bitmapSource.Format == PixelFormats.Pbgra32
                ? bitmapSource
                : new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);

            using var stream = File.Create(texturePath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(sourceToSave));
            encoder.Save(stream);

            if (ModelViewer3DConfig.Instance.HideExportedTextureFiles)
            {
                try
                {
                    File.SetAttributes(texturePath, File.GetAttributes(texturePath) | FileAttributes.Hidden);
                }
                catch
                {
                }
            }

            return textureFileName;
        }

        private static BitmapSource? ExtractBitmapSource(ImageBrush imageBrush)
        {
            if (imageBrush.ImageSource is BitmapSource bitmapSource)
                return bitmapSource;

            if (imageBrush.ViewboxUnits == BrushMappingMode.Absolute && imageBrush.ImageSource != null)
            {
                var image = new Image { Source = imageBrush.ImageSource, Stretch = Stretch.Fill };
                image.Measure(new Size(imageBrush.Viewbox.Width, imageBrush.Viewbox.Height));
                image.Arrange(new Rect(0, 0, imageBrush.Viewbox.Width, imageBrush.Viewbox.Height));
                var renderBitmap = new RenderTargetBitmap(
                    Math.Max((int)Math.Ceiling(imageBrush.Viewbox.Width), 1),
                    Math.Max((int)Math.Ceiling(imageBrush.Viewbox.Height), 1),
                    96,
                    96,
                    PixelFormats.Pbgra32);
                renderBitmap.Render(image);
                renderBitmap.Freeze();
                return renderBitmap;
            }

            return null;
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
