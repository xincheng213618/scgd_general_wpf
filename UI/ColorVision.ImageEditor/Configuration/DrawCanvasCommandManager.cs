using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Configuration
{
    /// <summary>
    /// DrawCanvas 命令管理器包装 - 桥接旧版 ActionCommand 和新的 ICommand 系统
    /// </summary>
    public class DrawCanvasCommandManager
    {
        private readonly DrawCanvas _canvas;
        private ICommandManager _commandManager;
        private bool _useNewCommandSystem = false;

        /// <summary>
        /// 是否使用新的命令系统
        /// </summary>
        public bool UseNewCommandSystem
        {
            get => _useNewCommandSystem;
            set
            {
                _useNewCommandSystem = value;
                if (value && _commandManager == null)
                {
                    _commandManager = new CommandManager();
                }
            }
        }

        /// <summary>
        /// 命令管理器
        /// </summary>
        public ICommandManager CommandManager
        {
            get => _commandManager;
            set
            {
                _commandManager = value;
                _useNewCommandSystem = value != null;
            }
        }

        public DrawCanvasCommandManager(DrawCanvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        /// <summary>
        /// 添加 Visual
        /// </summary>
        public void AddVisual(Visual visual)
        {
            if (_useNewCommandSystem && _commandManager != null)
            {
                var command = new AddVisualCommand(_canvas, visual);
                _commandManager.Execute(command);
            }
            else
            {
                // 使用旧版命令系统
                _canvas.AddVisualCommand(visual);
            }
        }

        /// <summary>
        /// 移除 Visual
        /// </summary>
        public void RemoveVisual(Visual visual)
        {
            if (_useNewCommandSystem && _commandManager != null)
            {
                var command = new RemoveVisualCommand(_canvas, visual);
                _commandManager.Execute(command);
            }
            else
            {
                // 使用旧版命令系统
                _canvas.RemoveVisualCommand(visual);
            }
        }

        /// <summary>
        /// 撤销
        /// </summary>
        public void Undo()
        {
            if (_useNewCommandSystem && _commandManager != null)
            {
                _commandManager.Undo();
            }
            else
            {
                _canvas.Undo();
            }
        }

        /// <summary>
        /// 重做
        /// </summary>
        public void Redo()
        {
            if (_useNewCommandSystem && _commandManager != null)
            {
                _commandManager.Redo();
            }
            else
            {
                _canvas.Redo();
            }
        }

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo
        {
            get
            {
                if (_useNewCommandSystem && _commandManager != null)
                {
                    return _commandManager.CanUndo;
                }
                return _canvas.UndoStack.Count > 0;
            }
        }

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo
        {
            get
            {
                if (_useNewCommandSystem && _commandManager != null)
                {
                    return _commandManager.CanRedo;
                }
                return _canvas.RedoStack.Count > 0;
            }
        }

        /// <summary>
        /// 清空历史
        /// </summary>
        public void ClearHistory()
        {
            if (_useNewCommandSystem && _commandManager != null)
            {
                _commandManager.ClearHistory();
            }
            else
            {
                _canvas.ClearActionCommand();
            }
        }
    }
}
