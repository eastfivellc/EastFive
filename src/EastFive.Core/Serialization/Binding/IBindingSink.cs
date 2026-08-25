using System;
using System.Collections.Generic;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Mirror of <see cref="IBindingSource"/> for the write side: a format-neutral
    /// place to deposit scalars and to descend into composite scopes. Format adapters
    /// (EDM row, JSON writer, query-string builder) ship their own implementation.
    /// <para>
    /// Writes are synchronous and void-returning. Value-shaped failure modes don't
    /// exist on this surface — a sink either accepts the value or the binder
    /// shouldn't have been picked. Shape-mismatch programmer errors (e.g., calling
    /// <c>Scope</c> on a scalar-only sink) throw <see cref="InvalidOperationException"/>.
    /// Async-bearing sinks (streamed serialization) can ship later without
    /// disturbing existing call sites because no binder relies on a return value.
    /// </para>
    /// </summary>
    public interface IBindingSink
    {
        void WriteString(string value);
        void WriteGuid(Guid value);
        void WriteBool(bool value);
        /// <summary>Write a 32-bit signed integer. Adapters with a narrow native
        /// wire type (e.g. EDM <c>Int32</c>) should preserve it; otherwise widen
        /// to the long path. <see cref="WriteInt64"/> remains the wide-integer write.</summary>
        void WriteInt32(int value);
        void WriteInt64(long value);
        void WriteDouble(double value);
        void WriteDateTime(DateTime value);
        void WriteBytes(byte[] value);

        /// <summary>Write an explicit null (sink decides the encoding: absent column, null sentinel, JSON null, …).</summary>
        void WriteNull();

        /// <summary>Open a child sink under <paramref name="key"/> for composite (object) writes.</summary>
        IBindingSink Scope(string key);

        /// <summary>Open a child sink for the next array element.</summary>
        IBindingSink AppendItem();
    }
}
