using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Spectrum.Update
{
    public class ChangeLogEntry : ViewModelBase
    {
        [DisplayName("Version")]
        public string Version { get; set; }
        [DisplayName("ReleaseDate")]
        public DateTime ReleaseDate { get; set; }
        public List<string> Changes { get; set; }
        [DisplayName("ChangeLog")]
        public string ChangeLog 
        {
            get 
            {
                _ChangeLog ??= string.Join("\n", Changes);
                return _ChangeLog;
            }
        }

        private string _ChangeLog;
        public RelayCommand UpdateCommand { get; set; }

        public bool IsUpdateAvailable => false;

        public bool IsCurrentVision => false;

        public string UpdateString => "";
        public ContextMenu ContextMenu { get; set; }
        public ChangeLogEntry()
        {
            Changes = new List<string>();
            ContextMenu = new ContextMenu();
        }
    }
}
