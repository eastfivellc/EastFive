namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Optional capability of an <see cref="IBindingSink"/>: declare a slot's
    /// composite shape before, or without, writing into it. Composite binders
    /// call it so that an object with no members or an array with no items is
    /// still emitted (JSON <c>{}</c> / <c>[]</c>) instead of disappearing.
    /// Mirrors <see cref="IKeyedBindingSource"/> on the read side: sinks with no
    /// encoding for an empty composite (e.g. an EDM row) simply don't implement
    /// it, and binders skip the call.
    /// <para>
    /// Calling <see cref="BeginObject"/> on a slot that is already an object (or
    /// <see cref="BeginArray"/> on an array) is a no-op; calling either on a slot
    /// of another shape throws <see cref="System.InvalidOperationException"/>.
    /// </para>
    /// </summary>
    public interface IShapedBindingSink : IBindingSink
    {
        /// <summary>Mark this slot as an object, even if no member is written.</summary>
        void BeginObject();

        /// <summary>Mark this slot as an array, even if no item is appended.</summary>
        void BeginArray();
    }
}
