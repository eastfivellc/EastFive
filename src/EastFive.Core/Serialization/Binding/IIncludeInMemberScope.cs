using System.Reflection;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Attribute-interface contract for declaring that a member participates in
    /// a specific binding scope. Implementations are typically carried on
    /// member-level attributes (e.g. <c>[ApiProperty]</c>, <c>[StorageProperty]</c>).
    /// Discovered via <see cref="EastFive.Reflection.AttributeExtensions.GetAttributesInterface"/>.
    ///
    /// <para>An attribute may participate in multiple scopes by implementing this
    /// interface multiple times with different <typeparamref name="TScope"/>
    /// arguments.</para>
    ///
    /// <para><see cref="IMemberPlanProvider"/> consults
    /// <see cref="Include"/> to decide whether a member contributes to the plan
    /// for a given scope, and <see cref="GetWireName"/> to choose the wire-name
    /// the binding source/sink will see for that member.</para>
    /// </summary>
    /// <typeparam name="TScope">A scope marker type implementing
    /// <see cref="IMemberScope"/>.</typeparam>
    public interface IIncludeInMemberScope<TScope>
        where TScope : IMemberScope
    {
        /// <summary>Returns true to include the member in the plan for
        /// <typeparamref name="TScope"/>.</summary>
        bool Include(MemberInfo member);

        /// <summary>Wire-name the member is exposed under in
        /// <typeparamref name="TScope"/>. Should fall back to the .NET member
        /// name when no override is configured.</summary>
        string GetWireName(MemberInfo member);
    }
}
