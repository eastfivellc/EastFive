using Xunit;

namespace EastFive.Azure.Tests;

/// <summary>Tests that re-initialize or swap the ambient configuration run alone, so a swap is never
/// observed — or undone — by a test running beside it.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AmbientConfigurationCollection
{
    public const string Name = "Ambient configuration";
}
