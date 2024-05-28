using ColorVision.Solution.V;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public partial class TreeViewControl
    {
        private void IniCommand()
        {
            ApplicationCommands.Delete.InputGestures.Add(new KeyGesture(Key.Delete, ModifierKeys.None, "Del"));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ExecutedCommand, (s,e) => { if (e.Parameter is VObject baseObject) e.CanExecute = false; }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, ExecutedCommand, CanExecuteCommand));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, ExecutedCommand, CanExecuteCommand));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, ExecutedCommand, CanExecuteCommand));
            CommandBindings.Add(new CommandBinding(Commands.ReName, ExecutedCommand, CanExecuteCommand));
        }

        #region 通用命令执行函数
        private void CanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter is VObject baseObject)
            {
                if (e.Command == ApplicationCommands.SelectAll)
                {
                    e.CanExecute = false;
                }
                else if (e.Command == ApplicationCommands.Copy)
                {
                    e.CanExecute = baseObject.CanCopy;
                }
                else if (e.Command == ApplicationCommands.Cut)
                {
                    e.CanExecute = false;
                }
                else if (e.Command == ApplicationCommands.Paste)
                {
                    e.CanExecute = false;
                }
                else if (e.Command == ApplicationCommands.Delete)
                {
                    e.CanExecute = baseObject.CanDelete;
                }
                else if (e.Command == Commands.ReName)
                {
                    e.CanExecute = baseObject.CanReName;
                }
            }
            else if (SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is VObject baseObject1)
            {
                if (e.Command == ApplicationCommands.SelectAll)
                {
                    e.CanExecute = false;
                }
                else if (e.Command == ApplicationCommands.Copy)
                {
                    e.CanExecute = baseObject1.CanCopy;
                }
                else if (e.Command == ApplicationCommands.Cut)
                {
                    e.CanExecute = false;
                }
                else if (e.Command == ApplicationCommands.Paste)
                {
                    e.CanExecute = false;
                }
                else if (e.Command == ApplicationCommands.Delete)
                {
                    e.CanExecute = baseObject1.CanDelete;
                }
                else if (e.Command == Commands.ReName)
                {
                    e.CanExecute = baseObject1.CanReName;
                }
            }

        }



        private void ExecutedCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Copy)
            {
                //this.DoCopy();
            }
            else if (e.Command == ApplicationCommands.Cut)
            {
                //this.DoCut();
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                //this.DoPaste();
            }
            else if (e.Command == ApplicationCommands.Delete)
            {
                if (e.Parameter != null)
                {
                    if (e.Parameter is VObject baseObject)
                    {
                        baseObject.Parent.RemoveChild(baseObject);
                    }
                }
                else
                {
                    if (SelectedTreeViewItem != null)
                    {
                        if (SelectedTreeViewItem.DataContext is VObject baseObject)
                        {
                            baseObject.Parent?.RemoveChild(baseObject);
                        }
                    }
                }
            }
            else if (e.Command == Commands.ReName)
            {
                if (e.Parameter != null)
                {
                    if (e.Parameter is VObject baseObject)
                    {
                        LastReNameObject = baseObject;
                        baseObject.IsEditMode = true;
                    }
                }
                else
                {
                    //没有数据的时候通过点击确认
                    if (SelectedTreeViewItem != null)
                    {
                        if (SelectedTreeViewItem.DataContext is VObject baseObject)
                        {
                            LastReNameObject = baseObject;
                            baseObject.IsEditMode = true;
                        }
                    }
                }
            }
            else
            {

            }

        }

        #endregion
    }

}