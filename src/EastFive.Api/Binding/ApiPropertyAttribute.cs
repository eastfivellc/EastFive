using System;
using System.Reflection;

using EastFive.Api.Binding.Scopes;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 declaration that a property or field participates in the HTTP
    /// binding scopes — <see cref="RequestBody"/>, <see cref="ResponseBody"/>,
    /// and (when <see cref="Mutable"/>) <see cref="PatchBody"/>.
    ///
    /// <para>Replaces V2's <c>IProvideApiValue</c> family for V3-bound entities.
    /// Members without this attribute (or any other
    /// <see cref="IIncludeInMemberScope{TScope}"/> attribute) are
    /// <em>not</em> bindable in V3 — there is no convention fallback.</para>
    ///
    /// <para>Example:
    /// <code>
    /// public class ChatAgent
    /// {
    ///     [ApiProperty(Name = "id", Mutable = false)] public Guid Id;
    ///     [ApiProperty(Name = "name")]                public string Name;
    /// }
    /// </code>
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ApiPropertyAttribute : Attribute,
        IIncludeInMemberScope<RequestBody>,
        IIncludeInMemberScope<ResponseBody>,
        IIncludeInMemberScope<PatchBody>,
        IIncludeInMemberScope<QueryString>
    {
        /// <summary>Wire name. Falls back to the .NET member name when empty.</summary>
        public string Name { get; set; }

        /// <summary>When false the member is excluded from
        /// <see cref="PatchBody"/> (PATCH cannot change it) but still
        /// participates in request/response shapes.</summary>
        public bool Mutable { get; set; } = true;

        bool IIncludeInMemberScope<RequestBody>.Include(MemberInfo member) => true;
        bool IIncludeInMemberScope<ResponseBody>.Include(MemberInfo member) => true;
        bool IIncludeInMemberScope<PatchBody>.Include(MemberInfo member) => Mutable;
        bool IIncludeInMemberScope<QueryString>.Include(MemberInfo member) => true;

        string IIncludeInMemberScope<RequestBody>.GetWireName(MemberInfo member) => Name ?? member.Name;
        string IIncludeInMemberScope<ResponseBody>.GetWireName(MemberInfo member) => Name ?? member.Name;
        string IIncludeInMemberScope<PatchBody>.GetWireName(MemberInfo member) => Name ?? member.Name;
        string IIncludeInMemberScope<QueryString>.GetWireName(MemberInfo member) => Name ?? member.Name;
    }
}
