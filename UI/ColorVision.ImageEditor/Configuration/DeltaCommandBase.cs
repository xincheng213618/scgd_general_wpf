using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// 差分命令基类
    /// </summary>
    public abstract class DeltaCommandBase : ViewModelBase, IDeltaCommand
    {
        public Guid Id { get; } = Guid.NewGuid();
        public abstract string Name { get; }
        public DateTime Timestamp { get; } = DateTime.Now;

        // 缓存差分数据，避免重复计算
        private IDeltaData _cachedDeltaData;

        public void Execute()
        {
            // 执行前捕获状态
            _cachedDeltaData = CaptureDelta();
            OnExecute();
        }

        public void Undo()
        {
            if (_cachedDeltaData != null)
            {
                ApplyDelta(_cachedDeltaData);
            }
            OnUndo();
        }

        public virtual void Redo()
        {
            // 默认重新执行
            OnExecute();
        }

        public IDeltaData GetDeltaData()
        {
            return _cachedDeltaData ?? CaptureDelta();
        }

        public abstract void ApplyDelta(IDeltaData deltaData);

        /// <summary>
        /// 捕获当前差分数据
        /// </summary>
        protected abstract IDeltaData CaptureDelta();

        /// <summary>
        /// 执行具体操作
        /// </summary>
        protected abstract void OnExecute();

        /// <summary>
        /// 撤销后的额外处理
        /// </summary>
        protected virtual void OnUndo() { }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Name}";
        }
    }

    /// <summary>
    /// 属性变更差分命令
    /// </summary>
    public class PropertyChangeCommand<TTarget, TValue> : DeltaCommandBase where TTarget : class
    {
        private readonly TTarget _target;
        private readonly string _propertyName;
        private readonly Func<TValue> _getter;
        private readonly Action<TValue> _setter;
        private readonly TValue _newValue;

        public override string Name => $"修改 {_propertyName}";

        public PropertyChangeCommand(TTarget target, string propertyName, Func<TValue> getter, Action<TValue> setter, TValue newValue)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _newValue = newValue;
        }

        protected override IDeltaData CaptureDelta()
        {
            return new PropertyDeltaData<TValue>
            {
                TargetId = _target.GetHashCode().ToString(),
                PropertyName = _propertyName,
                OldValue = _getter(),
                NewValue = _newValue
            };
        }

        protected override void OnExecute()
        {
            _setter(_newValue);
        }

        public override void ApplyDelta(IDeltaData deltaData)
        {
            if (deltaData is PropertyDeltaData<TValue> propertyDelta)
            {
                _setter((TValue)propertyDelta.OldValue);
            }
        }
    }

    /// <summary>
    /// 属性差分数据
    /// </summary>
    public class PropertyDeltaData<T> : IDeltaData
    {
        public string TargetId { get; set; }
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }

        public byte[] Serialize()
        {
            // 简单实现，实际使用时应使用更高效的序列化方式
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<PropertyDeltaData<T>>(json);
            if (deserialized != null)
            {
                TargetId = deserialized.TargetId;
                PropertyName = deserialized.PropertyName;
                OldValue = deserialized.OldValue;
                NewValue = deserialized.NewValue;
            }
        }
    }

    /// <summary>
    /// Visual 添加命令
    /// </summary>
    public class AddVisualCommand : DeltaCommandBase
    {
        private readonly DrawCanvas _canvas;
        private readonly System.Windows.Media.Visual _visual;

        public override string Name => "添加图形";

        public AddVisualCommand(DrawCanvas canvas, System.Windows.Media.Visual visual)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _visual = visual ?? throw new ArgumentNullException(nameof(visual));
        }

        protected override IDeltaData CaptureDelta()
        {
            return new VisualDeltaData
            {
                TargetId = _canvas.GetHashCode().ToString(),
                PropertyName = "Visuals",
                OldValue = null,
                NewValue = _visual
            };
        }

        protected override void OnExecute()
        {
            _canvas.AddVisual(_visual);
        }

        public override void ApplyDelta(IDeltaData deltaData)
        {
            // 撤销添加 = 移除
            if (_canvas.ContainsVisual(_visual))
            {
                _canvas.RemoveVisual(_visual);
            }
        }

        public override void Redo()
        {
            // 重做时重新添加
            OnExecute();
        }
    }

    /// <summary>
    /// Visual 移除命令
    /// </summary>
    public class RemoveVisualCommand : DeltaCommandBase
    {
        private readonly DrawCanvas _canvas;
        private readonly System.Windows.Media.Visual _visual;
        private int _originalIndex = -1;

        public override string Name => "移除图形";

        public RemoveVisualCommand(DrawCanvas canvas, System.Windows.Media.Visual visual)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _visual = visual ?? throw new ArgumentNullException(nameof(visual));
        }

        protected override IDeltaData CaptureDelta()
        {
            // 使用 LINQ 获取索引
            _originalIndex = System.Linq.Enumerable.Count(
                System.Linq.Enumerable.TakeWhile(_canvas.Visuals, v => v != _visual));
            if (_originalIndex >= _canvas.Visuals.Count)
                _originalIndex = -1; // 未找到

            return new VisualDeltaData
            {
                TargetId = _canvas.GetHashCode().ToString(),
                PropertyName = "Visuals",
                OldValue = _visual,
                NewValue = null,
                Metadata = new Dictionary<string, object> { { "Index", _originalIndex } }
            };
        }

        protected override void OnExecute()
        {
            _canvas.RemoveVisual(_visual);
        }

        public override void ApplyDelta(IDeltaData deltaData)
        {
            // 撤销移除 = 重新添加
            if (!_canvas.ContainsVisual(_visual))
            {
                if (_originalIndex >= 0 && _originalIndex < _canvas.Visuals.Count)
                {
                    // 需要在指定位置插入
                    _canvas.InsertVisual(_originalIndex, _visual);
                }
                else
                {
                    _canvas.AddVisual(_visual);
                }
            }
        }

        public override void Redo()
        {
            OnExecute();
        }
    }

    /// <summary>
    /// Visual 差分数据
    /// </summary>
    public class VisualDeltaData : IDeltaData
    {
        public string TargetId { get; set; }
        public string PropertyName { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }

        /// <summary>
        /// 额外元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        public byte[] Serialize()
        {
            // Visual 序列化需要特殊处理，这里仅做标记
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                TargetId,
                PropertyName,
                Metadata
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            // Visual 反序列化需要从其他存储恢复
            var json = System.Text.Encoding.UTF8.GetString(data);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<VisualDeltaData>(json);
            if (deserialized != null)
            {
                TargetId = deserialized.TargetId;
                PropertyName = deserialized.PropertyName;
                Metadata = deserialized.Metadata ?? new Dictionary<string, object>();
            }
        }
    }
}
