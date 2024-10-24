﻿using ColorVision.Common.MVVM;
using System.Windows;

namespace ColorVision.UI.Menus
{
    public static class MenuItemConstants
    {
        public const string File = "File";
        public const string Edit = "Edit";
        public const string View = "View";
        public const string Tool = "Tool";
        public const string Help = "Help";
    }

    public interface IMenuItem
    {
        public string? OwnerGuid { get; }
        public string? GuidId { get; }
        public int Order { get; }
        public string? Header { get; }
        public string? InputGestureText { get; }
        public object? Icon { get; }
        public RelayCommand? Command { get; }

        public Visibility Visibility { get; }
    }

}
