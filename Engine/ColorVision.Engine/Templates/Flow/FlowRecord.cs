#pragma warning disable CA1720
using ColorVision.Common.MVVM;
using ST.Library.UI.NodeEditor;
using System;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Flow
{
    public class FlowRecord:ViewModelBase
    {
        public FlowRecord(STNode sTNode)
        {
            Guid = sTNode.Guid;
            Name = sTNode.Title;
            DateTime date = DateTime.Now;
            DateTimeFlowRun = date;
            DateTimeRun = date;
            DateTimeStop = date;
        }

        public ContextMenu ContextMenu { get; set; }

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;
        public Guid Guid { get; set; }
        public string Name { get => _Name; set { _Name =value; NotifyPropertyChanged(); } }
        private string _Name;
        public DateTime DateTimeFlowRun { get => _DateTimeFlowRun; set { _DateTimeFlowRun = value; NotifyPropertyChanged(); } }
        private DateTime _DateTimeFlowRun;

        public DateTime DateTimeRun { get => _DateTimeRun; set { _DateTimeRun = value; NotifyPropertyChanged(); } }
        private DateTime _DateTimeRun;

        public DateTime DateTimeStop { get => _DateTimeStop; set { _DateTimeStop = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(RunTime)); NotifyPropertyChanged(nameof(FlowTime)); } }
        private DateTime _DateTimeStop;

        public TimeSpan RunTime { get => _DateTimeStop - _DateTimeRun; }
        public TimeSpan FlowTime { get => _DateTimeStop - _DateTimeFlowRun; }
    }
}
