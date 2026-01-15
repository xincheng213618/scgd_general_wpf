using System.Windows.Input;

namespace ColorVision.Engine
{

    //public class MenuEngine : MenuItemMenuBase
    //{
    //    public override string GuidId => EngineCommands.EngineGuidId;
    //    public override string Header => ColorVision.Engine.Properties.Resources.MenuEngine;
    //    public override int Order => 2;
    //}
    //public class MenuStartExecutionCommand : MenuItemBase
    //{
    //    public override string OwnerGuid => EngineCommands.EngineGuidId;
    //    public override string Header => ColorVision.Engine.Properties.Resources.StartExecition;
    //    public override int Order => 99;

    //    public override ICommand Command => EngineCommands.StartExecutionCommand;
    //    public override object? Icon => MenuItemIcon.TryFindResource("DIRun");

    //    public override string InputGestureText => "F6";
    //}

    //public class MenuStopExecutionCommand : MenuItemBase
    //{
    //    public override string OwnerGuid => EngineCommands.EngineGuidId;
    //    public override string Header => ColorVision.Engine.Properties.Resources.StopExecution;
    //    public override int Order => 99;
    //    public override ICommand Command => EngineCommands.StopExecutionCommand;
    //    public override object? Icon => MenuItemIcon.TryFindResource("DIRunPaused");
    //    public override string InputGestureText => "F7";
    //}

    //public class MenuTakePhotoCommand : MenuItemBase
    //{
    //    public override string OwnerGuid => EngineCommands.EngineGuidId;

    //    public override string Header => ColorVision.Engine.Properties.Resources.CaptureImage;
    //    public override int Order => 99;

    //    public override ICommand Command => EngineCommands.TakePhotoCommand;
    //    public override string InputGestureText => "Ctrl+T";
    //}


    public static class EngineCommands
    {
        public static string EngineGuidId => "Engine";

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
