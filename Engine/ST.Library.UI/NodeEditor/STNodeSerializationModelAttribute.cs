using System;

namespace ST.Library.UI.NodeEditor;

// Serialization identity is independent of whether a node is offered in the creation menu.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class STNodeSerializationModelAttribute : Attribute
{
    public string Model { get; }

    public STNodeSerializationModelAttribute(string model)
    {
        Model = model;
    }
}
