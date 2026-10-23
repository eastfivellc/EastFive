#nullable enable
namespace EastFive.Configuration
{
    /// <summary>
    /// Implemented by an attribute on a configuration type's ACCOUNT member: how the account value
    /// becomes the <c>{Account}</c> segment of generated secret names, for vendors whose ids are not
    /// valid segments as they stand (a Google client id ends in <c>.apps.googleusercontent.com</c>).
    /// The translation must be exact and type-specific — drop a constant affix, never substitute
    /// characters — so two accounts cannot collide on one secret. Its result is still validated: a
    /// value it cannot make valid is refused as an invalid account, exactly as without it. The
    /// member's own value is unchanged; only the secret name uses the segment.
    /// </summary>
    public interface IProvideSecretAccountSegment
    {
        string SecretAccountSegment(string account);
    }
}
