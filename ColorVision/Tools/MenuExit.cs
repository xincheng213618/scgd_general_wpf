using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Windows;

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

        public RelayCommand Command => new(A => Execute());
        public Visibility Visibility => Visibility.Visible;

        private static void Execute()
        {
            Environment.Exit(0);
        }
    }
}
