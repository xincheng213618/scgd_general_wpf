using System.Windows.Input;

namespace ColorVision.Engine
{
    public static class EngineCommands
    {
        /// <summary>
        /// 开始执行
        /// </summary>
        public static RoutedUICommand StartExecutionCommand { get; set; } = new RoutedUICommand(ColorVision.Engine.Properties.Resources.StartExecition, ColorVision.Engine.Properties.Resources.StartExecition, typeof(EngineCommands), new InputGestureCollection(new[] { new KeyGesture(Key.F6, ModifierKeys.None, "F6") }));
        public static RoutedUICommand StopExecutionCommand { get; set; } = new RoutedUICommand(ColorVision.Engine.Properties.Resources.StopExecution, ColorVision.Engine.Properties.Resources.StopExecution, typeof(EngineCommands), new InputGestureCollection(new[] { new KeyGesture(Key.F7, ModifierKeys.None, "F7") }));
        /// <summary>
        /// 拍照
        /// </summary>
        public static RoutedUICommand TakePhotoCommand { get; set; } = new RoutedUICommand(ColorVision.Engine.Properties.Resources.CaptureImage, ColorVision.Engine.Properties.Resources.CaptureImage, typeof(EngineCommands), new InputGestureCollection(new[] { new KeyGesture(Key.T, ModifierKeys.Control, "Crtl+T") }));
    }
}
