using log4net;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace ColorVision.UI;

/// <summary>
/// Stores the small, process-wide set of property editor registrations.
/// Exact type registrations win; matchers are evaluated in registration order.
/// </summary>
internal sealed class PropertyEditorRegistry
{
    private readonly ILog _log;
    private readonly ConcurrentDictionary<Type, Type> _exactEditors = new();
    private readonly object _matcherLock = new();
    private volatile EditorMatcher[] _matchers = [];

    public ConcurrentDictionary<Type, IPropertyEditor> Instances { get; } = new();

    public PropertyEditorRegistry(ILog log)
    {
        _log = log;
    }

    public void Register<TEditor>(Type targetType) where TEditor : IPropertyEditor, new()
    {
        ArgumentNullException.ThrowIfNull(targetType);
        _exactEditors[targetType] = typeof(TEditor);
    }

    public void Register<TEditor>(Func<Type, bool> matcher) where TEditor : IPropertyEditor, new()
    {
        ArgumentNullException.ThrowIfNull(matcher);
        lock (_matcherLock)
            _matchers = [.. _matchers, new EditorMatcher(matcher, typeof(TEditor))];
    }

    public Type? Find(Type propertyType)
    {
        ArgumentNullException.ThrowIfNull(propertyType);
        if (_exactEditors.TryGetValue(propertyType, out Type? editorType))
            return editorType;

        foreach (EditorMatcher registration in _matchers)
        {
            if (Matches(registration, propertyType))
                return registration.EditorType;
        }

        return null;
    }

    public List<Type> FindAll(Type propertyType)
    {
        ArgumentNullException.ThrowIfNull(propertyType);
        var editorTypes = new List<Type>();
        if (_exactEditors.TryGetValue(propertyType, out Type? editorType))
            editorTypes.Add(editorType);

        foreach (EditorMatcher registration in _matchers)
        {
            if (Matches(registration, propertyType) && !editorTypes.Contains(registration.EditorType))
                editorTypes.Add(registration.EditorType);
        }

        return editorTypes;
    }

    public IPropertyEditor GetOrCreate(Type editorType)
    {
        ArgumentNullException.ThrowIfNull(editorType);
        return Instances.GetOrAdd(editorType, static type =>
            Activator.CreateInstance(type) as IPropertyEditor
            ?? throw new InvalidOperationException($"Could not create property editor '{type.FullName}'."));
    }

    private bool Matches(EditorMatcher registration, Type propertyType)
    {
        try
        {
            return registration.Predicate(propertyType);
        }
        catch (Exception ex)
        {
            _log.Warn($"Property editor matcher '{registration.EditorType.FullName}' failed for '{propertyType.FullName}'.", ex);
            return false;
        }
    }

    private readonly record struct EditorMatcher(Func<Type, bool> Predicate, Type EditorType);
}
