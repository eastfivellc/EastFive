using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;

using EastFive.Api.Binding;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Bindings
{
    /// <summary>
    /// V3 <see cref="ITypeBinder"/> for <see cref="XmlDocument"/>. Reads the
    /// source as a string and parses it into a loaded <see cref="XmlDocument"/>,
    /// so a controller parameter typed <see cref="XmlDocument"/> receives the
    /// request payload already parsed — the XML peer of the framework's built-in
    /// "deserialize the body into this POCO" binders.
    /// <para>
    /// Pairs with the <c>[BodyXml]</c> selection attribute (which roots the
    /// parameter at the raw request body), but binds from any string-producing
    /// source, so <c>[Body(Name = "x")]</c> / <c>[Query]</c> over an XML-bearing
    /// value works too. A malformed payload surfaces as a bind failure
    /// (<see cref="ParseError"/> → HTTP 400), not an unhandled exception.
    /// </para>
    /// </summary>
    public sealed class XmlDocumentBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(XmlDocument);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath;
            return source.GetValue<TResult>(
                path: path,
                onNull: onNull,
                onString: xml =>
                {
                    try
                    {
                        var document = new XmlDocument();
                        document.LoadXml(xml);
                        return onBound(document);
                    }
                    catch (XmlException ex)
                    {
                        return onFailure(new BindFailure(
                            new ParseError($"invalid XML: {ex.Message}"), typeof(XmlDocument), path));
                    }
                },
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            sink.WriteString(((XmlDocument)value).OuterXml);
        }
    }

    /// <summary>
    /// Module initializer that registers <see cref="XmlDocumentBinder"/> with the
    /// V3 <see cref="TypeBinderRegistry"/>. Runs once per process when the
    /// EastFive.Api assembly is loaded — no application wiring required.
    /// </summary>
    internal static class XmlDocumentBinderModuleInitializer
    {
        #pragma warning disable CA2255 // intentional: framework-level binder registration
        [ModuleInitializer]
        #pragma warning restore CA2255
        internal static void Init()
        {
            TypeBinderRegistry.Register(new XmlDocumentBinder());
        }
    }
}
