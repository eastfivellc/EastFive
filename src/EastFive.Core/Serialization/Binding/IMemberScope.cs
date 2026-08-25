namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Marker interface for member-binding scopes. A "scope" identifies a
    /// distinct binding purpose (e.g. request body, response body, PATCH body,
    /// query string, storage row) under which a member of a complex type may
    /// be considered bindable. Different scopes can select different members of
    /// the same type and assign different wire names.
    ///
    /// <para>Concrete scope types are typically empty sealed classes used
    /// purely as type tokens for generic dispatch (see
    /// <see cref="IIncludeInMemberScope{TScope}"/>) and runtime keys to
    /// <see cref="IMemberPlanProvider.GetPlan"/>.</para>
    ///
    /// <para>Built-in scopes are declared by the consumer packages — e.g.
    /// <c>RequestBody</c>, <c>ResponseBody</c>, <c>PatchBody</c>,
    /// <c>QueryString</c> in EastFive.Api; <c>StorageRow</c> in EastFive.Azure.
    /// EastFive.Core defines only the marker.</para>
    /// </summary>
    public interface IMemberScope
    {
    }
}
