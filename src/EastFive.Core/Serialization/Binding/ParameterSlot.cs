using System;
using System.Reflection;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// <see cref="IBindingSlot"/> wrapping a <see cref="ParameterInfo"/>. Reserved for
    /// the v2 <c>MethodBinder</c>/<c>RequestEnvelopeBindingSource</c> unification (see
    /// plan); shipped in v1 so the slot abstraction is already in place.
    /// </summary>
    public sealed class ParameterSlot : IBindingSlot
    {
        private readonly ParameterInfo parameter;

        public ParameterSlot(ParameterInfo parameter)
        {
            this.parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public string Name => parameter.Name;

        public Type Type => parameter.ParameterType;

        public ICustomAttributeProvider Attributes => parameter;

        public bool HasDefaultValue => parameter.HasDefaultValue;

        public object DefaultValue => parameter.HasDefaultValue ? parameter.DefaultValue : null;

        public ParameterInfo Parameter => parameter;
    }
}
