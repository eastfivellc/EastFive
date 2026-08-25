namespace EastFive.Api.Binding
{
    /// <summary>
    /// Per-deserializer hand to the V3 envelope. Each existing v2 envelope
    /// (<c>JsonEnvelope</c>, <c>FormEnvelope</c>, <c>MultipartEnvelope</c>,
    /// <c>QueryOnlyEnvelope</c>, <c>RawEnvelope</c>) implements this alongside
    /// <see cref="IRequestEnvelope"/> so the V3 dispatcher can compose a
    /// <see cref="IRequestEnvelopeV3"/> from <c>(body, query, route, request)</c>
    /// without each envelope having to also carry route/request fields.
    /// </summary>
    public interface IRequestEnvelopeBody
    {
        bool TryGetBody<TBody>(out TBody body);
    }
}
