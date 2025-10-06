using System;

namespace ColorVision.Common.MVVM
{
    public class ActionCommand
    {
        public string Header { get; set; }

        public Action UndoAction { get; set; }
        public Action RedoAction { get; set; }

        public ActionCommand(Action undoAction, Action redoAction)
        {
            UndoAction = undoAction;
            RedoAction = redoAction;
        }
    }




}
