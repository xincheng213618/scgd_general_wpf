using System.Windows.Input;

namespace ColorVision.UI
{
    public static class Commands
    {
        /// <summary>
        /// 重命名
        /// </summary>
       public static RoutedUICommand ReName { get; set; } = new RoutedUICommand("重命名(_M)", "重命名(_M)", typeof(Commands),new InputGestureCollection(new[] { new KeyGesture(Key.F2, ModifierKeys.None, "F2") }));

        public static RoutedUICommand UndoHistory { get; set; } = new RoutedUICommand("UndoHistory", "UndoHistory", typeof(Commands), new InputGestureCollection());

    }
}
