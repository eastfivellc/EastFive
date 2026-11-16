#nullable enable
namespace EastFive.Configuration
{
    /// <summary>
    /// Implemented by an attribute on the member that names a configuration type's secrets — its
    /// ACCOUNT member, or the <see cref="IProvideConfigurationInstanceKey.InstanceKey"/> property of
    /// a keyed type whose instance key is that account: how the value becomes the <c>{Account}</c>
    /// segment of generated secret names, for ids that are not valid segments as they stand (a Google
    /// client id ends in <c>.apps.googleusercontent.com</c>). The translation must be INJECTIVE over
    /// the values the member can hold — drop a constant affix, or encode reversibly
    /// (<see cref="EncodedSecretAccountSegmentAttribute"/>) — so two accounts cannot collide on one
    /// secret; a lossy substitution of characters is not one. Its result is still validated: a value
    /// it cannot make valid is refused as an invalid account, exactly as without it. The member's
    /// own value is unchanged; only the secret name uses the segment.
    /// </summary>
    public interface IProvideSecretAccountSegment
    {
        string SecretAccountSegment(string account);
    }
}
