namespace EastFive.Api.Binding
{
    /// <summary>
    /// Optional seam implemented by request envelopes that can hand the V3
    /// dispatcher already-built, typed parameter values — bypassing selection
    /// and binding for the named parameters. Production envelopes never
    /// implement this; the interface exists so test envelopes can inject
    /// fully-constructed CLR values (including resources with mock drivers
    /// attached) without round-tripping through JSON or a body source.
    /// <para>
    /// Lookup is consulted by <see cref="MethodDispatcherV3"/> immediately
    /// before <see cref="EastFive.Serialization.Binding.TypeBindings.Default"/>
    /// would otherwise be invoked for a parameter. A hit short-circuits the
    /// bind path entirely; a miss leaves the normal selection/binding flow
    /// untouched.
    /// </para>
    /// </summary>
    public interface IParameterOverrideSource
    {
        /// <summary>
        /// If a pre-built typed value has been supplied for the named
        /// parameter, return it. The value must be assignment-compatible with
        /// the parameter type — the dispatcher will fail the request with 400
        /// (and a descriptive reason) on a type mismatch.
        /// </summary>
        bool TryGetParameterOverride(string parameterName, out object value);
    }
}
