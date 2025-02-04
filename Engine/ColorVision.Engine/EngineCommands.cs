using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.Engine
{
    public class MenuEngine : MenuItemMenuBase
    {
        public override string GuidId => EngineCommands.EngineGuidId;
        public override string Header => "Engine";
        public override int Order => 4;
    }
    public class MenuStartExecutionCommand : MenuItemBase
    {
        public override string OwnerGuid => EngineCommands.EngineGuidId;
        public override string Header => "开始执行(_S)";
        public override int Order => 99;

        public override ICommand Command => EngineCommands.StartExecutionCommand;
        public override string InputGestureText => "F5";
    }

    public class MenuStopExecutionCommand : MenuItemBase
    {
        public override string OwnerGuid => EngineCommands.EngineGuidId;
        public override string Header => "停止执行(_S)";
        public override int Order => 99;

        public override ICommand Command => EngineCommands.StopExecutionCommand;
        public override string InputGestureText => "F10";
    }

    public class MenuTakePhotoCommand : MenuItemBase
    {
        public override string OwnerGuid => EngineCommands.EngineGuidId;

        public override string Header => "拍照(_S)";
        public override int Order => 99;

        public override ICommand Command => EngineCommands.TakePhotoCommand;
        public override string InputGestureText => "Ctrl+P";
    }


    public static class EngineCommands
    {
        public static string EngineGuidId => "Engine";

        /// <summary>
        /// 开始执行
        /// </summary>
        public static RoutedUICommand StartExecutionCommand { get; set; } = new RoutedUICommand(
            "开始执行(_S)",
            "开始执行(_S)",
            typeof(EngineCommands),
            new InputGestureCollection(new[] { new KeyGesture(Key.F5, ModifierKeys.None, "F5") })
        );
        public static RoutedUICommand StopExecutionCommand { get; set; } = new RoutedUICommand(
    "停止执行(_S)",
    "停止执行(_S)",
    typeof(EngineCommands),
    new InputGestureCollection(new[] { new KeyGesture(Key.F10, ModifierKeys.None, "F10") })
);

        /// <summary>
        /// 拍照
        /// </summary>
        public static RoutedUICommand TakePhotoCommand { get; set; } = new RoutedUICommand(
            "拍照(_P)",
            "拍照(_P)",
            typeof(EngineCommands),
            new InputGestureCollection(new[] { new KeyGesture(Key.P, ModifierKeys.Control, "Ctrl+P") })
        );
    }
}
