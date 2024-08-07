﻿using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects.ProjectHeyuan
{



    public class ProjectHeyuanExport : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "HeYuan";

        public int Order => 100;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "河源精电";
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
            new ProjectHeyuanWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
