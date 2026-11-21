using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Xunit;

using EastFive.Azure.Auth;
using EastFive.Azure.Communications;
using EastFive.Azure.Functions;
using EastFive.Azure.Login;
using EastFive.Azure.Persistence;
using EastFive.Azure.Persistence.StorageTables;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Persistence.Azure.StorageTables.Caching;

namespace EastFive.Azure.Tests;

/// <summary>
/// Every table type EastFive.Azure declares either saves its updates conditionally (a public
/// member whose attribute implements <see cref="IModifyAzureStorageTableETag"/>, as the driver
/// requires) or is listed here with the reason it does not. This stands in for the E5001
/// analyzer, which does not run in EastFive.Azure's own build: referencing EastFive.Generators
/// there would also make its source generators emit code into the library.
/// </summary>
public class StorageTableETagCoverageTests
{
    /// <summary>Decided: no ETag member, for the reason given.</summary>
    private static readonly IReadOnlyDictionary<Type, string> Exempt = new Dictionary<Type, string>
    {
        [typeof(ExecutionResult)] =
            "Created with a fresh secure id and updated once by the invocation that made it; no second caller has the id.",
        [typeof(CacheItem)] =
            "A cache: its mutation makes the HTTP request before saving, so it must not re-run, and losing one cached version is acceptable.",
        [typeof(AcsCallAutomationEvent)] =
            "A received event saved by create-or-replace of an instance that was not read; a repeated delivery writes the same event.",
        [typeof(Claim)] = "Created and deleted only.",
        [typeof(Client)] = "Created only.",
        [typeof(EastFive.Azure.Auth.Redirection)] = "Created only, one row per sign-in attempt.",
        [typeof(EventMessageLine)] = "Created only, with a fresh id per line.",
        [typeof(Integration.AuthorizationIntegrationLookup)] =
            "Created (a collision means the authorization is already in use) and deleted only.",
        [typeof(Invite)] = "No code in the framework reads or writes it.",
        [typeof(Redirect)] = "No code in the framework reads or writes it.",
        [typeof(AccountMappingOld)] = "A legacy table no code in the framework reads or writes.",
        [typeof(AccountMappingOld.AccountMappingLookup)] =
            "Nested in the legacy AccountMappingOld; no code in the framework reads or writes it.",
        [typeof(AccountMappingOld.AuthorizationLookup)] =
            "Nested in the legacy AccountMappingOld; no code in the framework reads or writes it.",
        [typeof(StorageTable)] = "Describes a table for GET endpoints; never stored.",
        [typeof(StorageProperty)] = "Describes a column for GET endpoints; never stored.",
        [typeof(StorageLinkAttribute.StorageLookupTable)] =
            "Only read; the attribute writes the referenced entity, not this table.",
        [typeof(StorageConstraintUniqueAttribute.StorageLookupTable)] =
            "A uniqueness claim taken by a create that collides; an update rewrites values equal to the ones it read. " +
            "A member would make the attribute's rollback deletes conditional, and a conditional delete that loses throws.",
    };

    /// <summary>Not decided: an ETag member would be wrong as the code stands, and so would exempting it.</summary>
    private static readonly IReadOnlyDictionary<Type, string> LeftVisible = new Dictionary<Type, string>
    {
        [typeof(Integration)] =
            "Its PATCH creates an AuthorizationIntegrationLookup inside the mutation; a re-run would collide with that " +
            "create and answer that the authorization is already in use.",
    };

    private static IEnumerable<Type> TableTypes() =>
        typeof(Authorization).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttributes(inherit: true).OfType<IProvideEntity>().Any());

    /// <summary>
    /// A sign-in callback controller (AppleRedirect, SAMLRedirect and the like) derives from
    /// <see cref="EastFive.Azure.Auth.Redirection"/> and so inherits its table attribute, but is
    /// never stored as itself; the analyzer, which reads declared attributes only, does not see
    /// them.
    /// </summary>
    private static bool IsRedirectionController(Type type) =>
        type != typeof(EastFive.Azure.Auth.Redirection)
        && typeof(EastFive.Azure.Auth.Redirection).IsAssignableFrom(type)
        && !type.GetCustomAttributes(inherit: false).OfType<IProvideEntity>().Any();

    private static bool HasPublicETagMember(Type type) =>
        type.GetMembers()
            .Any(member => member.GetCustomAttributes(inherit: true).OfType<IModifyAzureStorageTableETag>().Any());

    [Fact]
    public void EveryTableType_HasAPublicETagMember_OrIsListed()
    {
        var unlisted = TableTypes()
            .Where(type => !HasPublicETagMember(type))
            .Where(type => !IsRedirectionController(type))
            .Where(type => !Exempt.ContainsKey(type) && !LeftVisible.ContainsKey(type))
            .Select(type => type.FullName)
            .OrderBy(name => name)
            .ToArray();

        Assert.True(unlisted.Length == 0,
            "These table types have no public [ETag] member. Add `[ETag] [JsonIgnore] public string eTag;` " +
            "once every mutation of the type is safe to run twice, or list it here with the reason: " +
            string.Join(", ", unlisted));
    }

    [Fact]
    public void EveryListedType_IsATableTypeWithoutAnETagMember()
    {
        var tables = TableTypes().ToHashSet();
        var stale = Exempt.Keys
            .Concat(LeftVisible.Keys)
            .Where(type => !tables.Contains(type) || HasPublicETagMember(type))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(stale.Length == 0, "Listed but no longer a table type without an ETag member: " +
            string.Join(", ", stale));
        Assert.Empty(Exempt.Keys.Intersect(LeftVisible.Keys));
        Assert.All(Exempt.Concat(LeftVisible), entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
    }
}
