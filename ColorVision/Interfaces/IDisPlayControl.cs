﻿using ColorVision.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Interfaces
{
    public interface IDisPlayControl
    {
        public event RoutedEventHandler Selected;

        public event RoutedEventHandler Unselected;

        public event EventHandler SelectChanged;

        public bool IsSelected { get; set; }
    }

    public class DisPlayManager
    {
        private static DisPlayManager _instance;
        private static readonly object _locker = new();
        public static DisPlayManager GetInstance() { lock (_locker) { return _instance ??= new DisPlayManager(); } }
        public ObservableCollection<IDisPlayControl> IDisPlayControls { get; private set; }

        private DisPlayManager()
        {
            IDisPlayControls = new ObservableCollection<IDisPlayControl>();
        }


    }
}