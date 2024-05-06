using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;

namespace ColorVision.Tools
{
    public class MenuExit : IMenuItem
    {
        public string? OwnerGuid => "File";

        public string? GuidId => "MenuExit";

        public int Order => 1000000;

        public string? Header => ColorVision.Properties.Resource.MenuExit;

        public string? InputGestureText => "Alt + F4";

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(A => Execute());

        private static void Execute()
        {
            Environment.Exit(0);
        }
    }
}
