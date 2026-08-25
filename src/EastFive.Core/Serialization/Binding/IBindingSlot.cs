using System;
using System.Reflection;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Format-neutral abstraction over reflection slots that can be bound to:
    /// <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>, and (in a later phase)
    /// <see cref="ParameterInfo"/>. Used by <see cref="ITypeBindings.ForSlot"/> so
    /// composite binders (e.g. <c>PocoBinder</c>) never inspect reflection metadata
    /// themselves — per-slot overrides live entirely in the <c>ForSlot</c> overlay.
    /// </summary>
    public interface IBindingSlot
    {
        string Name { get; }

        Type Type { get; }

        ICustomAttributeProvider Attributes { get; }

        bool HasDefaultValue { get; }

        object DefaultValue { get; }
    }
}
