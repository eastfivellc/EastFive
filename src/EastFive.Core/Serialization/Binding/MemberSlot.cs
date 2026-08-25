using System;
using System.Reflection;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// <see cref="IBindingSlot"/> wrapping a <see cref="PropertyInfo"/> or
    /// <see cref="FieldInfo"/>. Used by <c>PocoBinder</c> when walking a complex
    /// type's members.
    /// </summary>
    public sealed class MemberSlot : IBindingSlot
    {
        private readonly MemberInfo member;

        public MemberSlot(PropertyInfo property)
        {
            member = property ?? throw new ArgumentNullException(nameof(property));
            Name = property.Name;
            Type = property.PropertyType;
        }

        public MemberSlot(FieldInfo field)
        {
            member = field ?? throw new ArgumentNullException(nameof(field));
            Name = field.Name;
            Type = field.FieldType;
        }

        public string Name { get; }

        public Type Type { get; }

        public ICustomAttributeProvider Attributes => member;

        public bool HasDefaultValue => false;

        public object DefaultValue => null;

        public MemberInfo Member => member;
    }
}
