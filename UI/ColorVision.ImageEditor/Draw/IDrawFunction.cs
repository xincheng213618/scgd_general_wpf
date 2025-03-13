#pragma warning disable CS8625
using System.Drawing;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.Draw
{

    public interface IToolCommand
    {
        public string? OwnerGuid { get; }
        public string? GuidId { get; }
        public int Order { get; }
        public string? Header { get; }

        public object? Icon { get; }
        public string? Description { get; }

        public ICommand? Command { get; }
    }

    public abstract class IToolCommandBase : IToolCommand
    {
        public virtual string? OwnerGuid => "1";

        public virtual string? GuidId => GetType().Name;

        public int Order => 1;

        public virtual string? Header => string.Empty;

        public virtual object? Icon => null;

        public virtual string? Description => null;

        public ICommand? Command => null;
    }


}
