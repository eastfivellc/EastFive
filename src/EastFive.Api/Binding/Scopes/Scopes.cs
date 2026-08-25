using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding.Scopes
{
    /// <summary>Scope marker for HTTP request-body deserialization (POST/PUT
    /// inserts). Members tagged for this scope are read off the JSON body when
    /// constructing a new resource.</summary>
    public sealed class RequestBody : IMemberScope { }

    /// <summary>Scope marker for HTTP response-body serialization. Members
    /// tagged for this scope appear in the wire payload returned to the
    /// client.</summary>
    public sealed class ResponseBody : IMemberScope { }

    /// <summary>Scope marker for HTTP PATCH-body partial mutation. Members
    /// tagged for this scope can be set via a <c>MutateResource&lt;T&gt;</c>
    /// produced by <c>[MutateEntity]</c>.</summary>
    public sealed class PatchBody : IMemberScope { }

    /// <summary>Scope marker for binding from URL query string / route values
    /// / headers — flat scalar-or-array sources rather than a JSON body.</summary>
    public sealed class QueryString : IMemberScope { }
}
