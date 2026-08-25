using System;
using System.Globalization;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>
    /// Binds C# primitive scalars: string, bool, int family, double/float/decimal,
    /// Uri, char. Each target accepts only the native callbacks that can produce
    /// it without loss; cross-type coercion (string → int, int → bool, etc.) is
    /// implemented here.
    /// </summary>
    public sealed class PrimitiveBinder : ITypeBinder
    {
        public bool CanBind(Type t) =>
            t == typeof(string) ||
            t == typeof(bool) ||
            t == typeof(byte) || t == typeof(sbyte) ||
            t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) ||
            t == typeof(long) || t == typeof(ulong) ||
            t == typeof(float) || t == typeof(double) || t == typeof(decimal) ||
            t == typeof(Uri) || t == typeof(char);

        public ValueTask<TResult> Read<TResult>(
            Type t,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath;

            if (t == typeof(string))
            {
                return source.GetValue<TResult>(
                    path: path,
                    onNull: onNull,
                    onString: s => onBound(s),
                    onGuid: g => onBound(g.ToString()),
                    onInt64: i => onBound(i.ToString(CultureInfo.InvariantCulture)),
                    onDouble: d => onBound(d.ToString("R", CultureInfo.InvariantCulture)),
                    onDateTime: dt => onBound(dt.ToString("O", CultureInfo.InvariantCulture)),
                    onFailure: onFailure);
            }

            if (t == typeof(bool))
            {
                return source.GetValue<TResult>(
                    path: path,
                    onNull: onNull,
                    onBool: b => onBound(b),
                    onString: s => TryParseBool(s, out var b)
                        ? onBound(b)
                        : onFailure(new BindFailure(new ParseError($"'{s}' is not a bool"), t, path)),
                    onInt64: i => onBound(i != 0),
                    onFailure: onFailure);
            }

            if (IsIntegral(t))
            {
                return source.GetValue<TResult>(
                    path: path,
                    onNull: onNull,
                    onInt64: i => TryNarrowIntegral(t, i, onBound, onFailure, path),
                    onString: s =>
                    {
                        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                            return TryNarrowIntegral(t, i, onBound, onFailure, path);
                        return onFailure(new BindFailure(new ParseError($"'{s}' is not an {t.Name}"), t, path));
                    },
                    onFailure: onFailure);
            }

            if (t == typeof(double) || t == typeof(float) || t == typeof(decimal))
            {
                return source.GetValue<TResult>(
                    path: path,
                    onNull: onNull,
                    onDouble: d => onBound(NarrowFloating(t, d)),
                    onInt64: i => onBound(NarrowFloating(t, (double)i)),
                    onString: s =>
                    {
                        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                            return onBound(NarrowFloating(t, d));
                        return onFailure(new BindFailure(new ParseError($"'{s}' is not a {t.Name}"), t, path));
                    },
                    onFailure: onFailure);
            }

            if (t == typeof(Uri))
            {
                return source.GetValue<TResult>(
                    path: path,
                    onNull: onNull,
                    onString: s => Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var u)
                        ? onBound(u)
                        : onFailure(new BindFailure(new ParseError($"'{s}' is not a Uri"), t, path)),
                    onFailure: onFailure);
            }

            if (t == typeof(char))
            {
                return source.GetValue<TResult>(
                    path: path,
                    onNull: onNull,
                    onString: s => s is { Length: 1 }
                        ? onBound(s[0])
                        : onFailure(new BindFailure(new ParseError($"'{s}' is not a single char"), t, path)),
                    onFailure: onFailure);
            }

            return new ValueTask<TResult>(onFailure(new BindFailure(new UnsupportedTargetType(t), t, path)));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }

            if (sourceType == typeof(string)) { sink.WriteString((string)value); return; }
            if (sourceType == typeof(bool)) { sink.WriteBool((bool)value); return; }
            if (sourceType == typeof(int)) { sink.WriteInt32((int)value); return; }
            if (sourceType == typeof(uint)) { sink.WriteInt64((uint)value); return; }
            if (sourceType == typeof(short)) { sink.WriteInt32((short)value); return; }
            if (sourceType == typeof(ushort)) { sink.WriteInt32((ushort)value); return; }
            if (sourceType == typeof(byte)) { sink.WriteInt32((byte)value); return; }
            if (sourceType == typeof(sbyte)) { sink.WriteInt32((sbyte)value); return; }
            if (sourceType == typeof(long)) { sink.WriteInt64((long)value); return; }
            if (sourceType == typeof(ulong)) { sink.WriteInt64((long)(ulong)value); return; }
            if (sourceType == typeof(double)) { sink.WriteDouble((double)value); return; }
            if (sourceType == typeof(float)) { sink.WriteDouble((float)value); return; }
            if (sourceType == typeof(decimal)) { sink.WriteDouble((double)(decimal)value); return; }
            if (sourceType == typeof(Uri)) { sink.WriteString(((Uri)value).ToString()); return; }
            if (sourceType == typeof(char)) { sink.WriteString(((char)value).ToString()); return; }
            throw new InvalidOperationException($"PrimitiveBinder cannot write {sourceType}");
        }

        private static bool IsIntegral(Type t) =>
            t == typeof(byte) || t == typeof(sbyte) ||
            t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) ||
            t == typeof(long) || t == typeof(ulong);

        private static bool TryParseBool(string s, out bool result)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "true": case "t": case "yes": case "y": case "on": case "1":
                    result = true; return true;
                case "false": case "f": case "no": case "n": case "off": case "0":
                    result = false; return true;
            }
            result = false;
            return false;
        }

        private static TResult TryNarrowIntegral<TResult>(Type t, long i, Func<object, TResult> onBound, Func<BindFailure, TResult> onFailure, string path)
        {
            try
            {
                if (t == typeof(int)) return onBound(checked((int)i));
                if (t == typeof(uint)) return onBound(checked((uint)i));
                if (t == typeof(short)) return onBound(checked((short)i));
                if (t == typeof(ushort)) return onBound(checked((ushort)i));
                if (t == typeof(byte)) return onBound(checked((byte)i));
                if (t == typeof(sbyte)) return onBound(checked((sbyte)i));
                if (t == typeof(long)) return onBound(i);
                if (t == typeof(ulong)) return onBound(checked((ulong)i));
            }
            catch (OverflowException)
            {
                return onFailure(new BindFailure(new ParseError($"{i} overflows {t.Name}"), t, path));
            }
            return onFailure(new BindFailure(new UnsupportedTargetType(t), t, path));
        }

        private static object NarrowFloating(Type t, double d)
        {
            if (t == typeof(double)) return d;
            if (t == typeof(float)) return (float)d;
            if (t == typeof(decimal)) return (decimal)d;
            return d;
        }
    }
}
