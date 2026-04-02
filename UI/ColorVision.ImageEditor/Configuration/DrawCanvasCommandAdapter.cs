using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// DrawCanvas 命令适配器 - 帮助迁移到新的命令系统
    /// </summary>
    public static class DrawCanvasCommandAdapter
    {
        /// <summary>
        /// 创建添加 Visual 的命令
        /// </summary>
        public static ICommand CreateAddVisualCommand(this DrawCanvas canvas, Visual visual)
        {
            return new AddVisualCommand(canvas, visual);
        }

        /// <summary>
        /// 创建移除 Visual 的命令
        /// </summary>
        public static ICommand CreateRemoveVisualCommand(this DrawCanvas canvas, Visual visual)
        {
            return new RemoveVisualCommand(canvas, visual);
        }

        /// <summary>
        /// 执行添加 Visual 命令（自动加入 Undo 栈）
        /// </summary>
        public static void ExecuteAddVisual(this DrawCanvas canvas, Visual visual, ICommandManager commandManager)
        {
            if (commandManager == null)
            {
                // 回退到直接操作
                canvas.AddVisual(visual);
                return;
            }

            var command = canvas.CreateAddVisualCommand(visual);
            commandManager.Execute(command);
        }

        /// <summary>
        /// 执行移除 Visual 命令（自动加入 Undo 栈）
        /// </summary>
        public static void ExecuteRemoveVisual(this DrawCanvas canvas, Visual visual, ICommandManager commandManager)
        {
            if (commandManager == null)
            {
                // 回退到直接操作
                canvas.RemoveVisual(visual);
                return;
            }

            var command = canvas.CreateRemoveVisualCommand(visual);
            commandManager.Execute(command);
        }

        /// <summary>
        /// 批量添加 Visuals（使用事务）
        /// </summary>
        public static void ExecuteAddVisualsBatch(this DrawCanvas canvas, Visual[] visuals, ITransactionalCommandManager commandManager)
        {
            if (commandManager == null)
            {
                foreach (var visual in visuals)
                {
                    canvas.AddVisual(visual);
                }
                return;
            }

            commandManager.BeginTransaction("批量添加图形");
            try
            {
                foreach (var visual in visuals)
                {
                    var command = canvas.CreateAddVisualCommand(visual);
                    commandManager.Execute(command);
                }
                commandManager.CommitTransaction();
            }
            catch
            {
                commandManager.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// 批量移除 Visuals（使用事务）
        /// </summary>
        public static void ExecuteRemoveVisualsBatch(this DrawCanvas canvas, Visual[] visuals, ITransactionalCommandManager commandManager)
        {
            if (commandManager == null)
            {
                foreach (var visual in visuals)
                {
                    canvas.RemoveVisual(visual);
                }
                return;
            }

            commandManager.BeginTransaction("批量移除图形");
            try
            {
                foreach (var visual in visuals)
                {
                    var command = canvas.CreateRemoveVisualCommand(visual);
                    commandManager.Execute(command);
                }
                commandManager.CommitTransaction();
            }
            catch
            {
                commandManager.RollbackTransaction();
                throw;
            }
        }
    }

    /// <summary>
    /// Visual 变换命令 - 支持移动、缩放、旋转
    /// </summary>
    public class VisualTransformCommand : DeltaCommandBase
    {
        private readonly DrawCanvas _canvas;
        private readonly Visual _visual;
        private readonly Transform _oldTransform;
        private readonly Transform _newTransform;

        public override string Name => "变换图形";

        public VisualTransformCommand(DrawCanvas canvas, Visual visual, Transform oldTransform, Transform newTransform)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _visual = visual ?? throw new ArgumentNullException(nameof(visual));
            _oldTransform = oldTransform;
            _newTransform = newTransform;
        }

        protected override IDeltaData CaptureDelta()
        {
            return new VisualTransformDeltaData
            {
                TargetId = _visual.GetHashCode().ToString(),
                OldTransform = _oldTransform?.Value ?? Matrix.Identity,
                NewTransform = _newTransform?.Value ?? Matrix.Identity
            };
        }

        protected override void OnExecute()
        {
            ApplyTransform(_newTransform);
        }

        public override void ApplyDelta(IDeltaData deltaData)
        {
            if (deltaData is VisualTransformDeltaData transformDelta)
            {
                var transform = new MatrixTransform(transformDelta.OldTransform);
                ApplyTransform(transform);
            }
        }

        protected override void OnUndo()
        {
            // ApplyDelta 已经处理了撤销逻辑
        }

        public override void Redo()
        {
            ApplyTransform(_newTransform);
        }

        private void ApplyTransform(Transform transform)
        {
            if (_visual is UIElement element && transform != null)
            {
                element.RenderTransform = transform;
            }
        }
    }

    /// <summary>
    /// Visual 变换差分数据
    /// </summary>
    public class VisualTransformDeltaData : IDeltaData
    {
        public string TargetId { get; set; }
        public string PropertyName => "RenderTransform";
        public object OldValue { get; set; }
        public object NewValue { get; set; }

        public Matrix OldTransform { get; set; }
        public Matrix NewTransform { get; set; }

        public byte[] Serialize()
        {
            // 简化的序列化实现
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                TargetId,
                OldTransform = OldTransform.ToString(),
                NewTransform = NewTransform.ToString()
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            // 简化的反序列化实现
            var json = System.Text.Encoding.UTF8.GetString(data);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<VisualTransformDeltaData>(json);
            if (deserialized != null)
            {
                TargetId = deserialized.TargetId;
                OldTransform = deserialized.OldTransform;
                NewTransform = deserialized.NewTransform;
            }
        }
    }
}
