using ColorVision.ImageEditor.EditorTools.Filters;
using System;
using System.ComponentModel;
using System.Windows.Media.Effects;

namespace ColorVision.ImageEditor.Presentation;

/// <summary>Owns the per-view shader capability independently of toolbar creation and disposal.</summary>
public sealed class ImageShaderPresentation : IDisposable
{
    private readonly ImagePresentation _presentation;
    private readonly DisplayShaderFilterEffect? _effect;
    private Effect? _previousEffect;
    private bool _attached;
    private bool _disposed;

    internal ImageShaderPresentation(ImagePresentation presentation)
    {
        _presentation = presentation;
        State = new DisplayShaderFilterState();
        State.CopyFrom(DisplayShaderFilterDefaultConfig.Current.State);
        if (DisplayShaderFilterEnvironment.Current.CanUseShaderFilter)
            _effect = new DisplayShaderFilterEffect();
        State.PropertyChanged += OnStateChanged;
        Apply();
    }

    public DisplayShaderFilterState State { get; }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e) => Apply();

    private void Apply()
    {
        if (_disposed) return;
        _effect?.Apply(State);
        if (State.IsEnabled && _effect != null)
        {
            if (!_attached)
            {
                _previousEffect = _presentation.SceneEffect;
                _attached = true;
            }
            // Existing shader filters affect the complete scene. Keep that compatibility
            // explicit instead of silently changing annotation and screenshot appearance.
            _presentation.SetSceneEffect(_effect);
        }
        else Detach();
    }

    private void Detach()
    {
        if (!_attached) return;
        if (_presentation.SceneEffect == _effect) _presentation.SetSceneEffect(_previousEffect);
        _previousEffect = null;
        _attached = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        State.PropertyChanged -= OnStateChanged;
        Detach();
    }
}
