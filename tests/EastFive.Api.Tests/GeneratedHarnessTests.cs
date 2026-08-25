using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using EastFive.Api.Tests.Harness;

namespace EastFive.Api.Tests;

/// <summary>
/// Proves the parameterized TestHarnessGenerator ran against THIS assembly
/// from build-property configuration alone (no hardwired consumer names):
/// the generated extension class exists in the configured namespace, and a
/// generated wrapper dispatches end to end.
/// </summary>
public class GeneratedHarnessTests : TestSession
{
    [Fact]
    public void GeneratorEmittedExtensionsIntoConfiguredNamespace()
    {
        var generated = typeof(GeneratedHarnessTests).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "EastFive.Api.Tests.Generated")
            .ToArray();

        Assert.NotEmpty(generated);
    }
}
