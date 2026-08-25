# Source Generator for InvokeDelayed Overloads

This document explains how the source generator works to create multiple overloads of the `InvokeDelayed` method.

## Overview

The `InvokeDelayedGenerator` is a C# source generator that automatically creates overloads of the `InvokeDelayed` extension method for different combinations of prime parameters and delayed parameters.

## Generated Combinations

The generator creates overloads for these combinations:
- **0 primes**: 1-4 delayed parameters  
  `Func<TDelayed1, ..., Task<TResult>> → Func<Task<TResult>>`
  
- **1 prime**: 1-4 delayed parameters  
  `Func<TPrime1, TDelayed1, ..., Task<TResult>> → Func<TPrime1, Task<TResult>>`
  
- **2 primes**: 1-3 delayed parameters (2-4 is manually defined)  
  `Func<TPrime1, TPrime2, TDelayed1, ..., Task<TResult>> → Func<TPrime1, TPrime2, Task<TResult>>`
  
- **3 primes**: 1-4 delayed parameters  
  `Func<TPrime1, TPrime2, TPrime3, TDelayed1, ..., Task<TResult>> → Func<TPrime1, TPrime2, TPrime3, Task<TResult>>`

## How It Works

### 1. Generator Project Structure

```
EastFive.Core.Generators/
├── EastFive.Core.Generators.csproj  (targets netstandard2.0)
└── InvokeDelayedGenerator.cs        (implements ISourceGenerator)
```

###2. Key Components

**Generator Class:**
```csharp
[Generator]
public class InvokeDelayedGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        var source = GenerateInvokeDelayedOverloads();
        context.AddSource("DiscriminatedFunctions.InvokeDelayed.g.cs", source);
    }
}
```

**Generated Code Pattern:**
Each overload follows this template:
```csharp
public static Func<TPrime1, ..., Task<TResult>> InvokeDelayed<TPrime1, ..., TDelayed1, ..., TResult>(
    this Func<TPrime1, ..., TDelayed1, ..., Task<TResult>> func,
    InvokeDelayedAsyncCallback<TDelayed1, TResult> delayed1Callback,
    ...)
{
    // Create TaskCompletionSource for each delayed parameter
    // Start all callbacks in parallel using RunDelayedCallback helper
    // Return async lambda that races between short-circuit and completion
}
```

### 3. Integration with EastFive.Core

The `EastFive.Core.csproj` references the generator as an analyzer:

```xml
<ItemGroup>
  <ProjectReference Include="..\EastFive.Core.Generators\EastFive.Core.Generators.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

The main class is marked as `partial`:

```csharp
public static partial class DiscriminatedFunctions
{
    // Manual implementations (like 2 primes, 4 delayed)
    // ...
}
```

## Benefits

1. **DRY Principle**: Write the logic once, generate many overloads
2. **Compile-Time Generation**: Zero runtime overhead
3. **Type Safety**: All overloads are strongly typed
4. **Maintainability**: Update one template, all overloads update
5. **Extensibility**: Easy to add more combinations by modifying the array

## Usage Example

```csharp
// Example with 1 prime, 2 delayed parameters (auto-generated)
Func<IRef<Account>, IRef<Practice>, IRef<Department>, Task<HttpResponseMessage>> processRequest = ...;

var optimized = processRequest.InvokeDelayed(
    async (onFound) => await account.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await practice.StorageGetAsync(onFound, () => NotFound())
);

// Now call with just the prime parameter
var response = await optimized(departmentRef);
```

## Viewing Generated Code

During build, the generated code is placed in:
```
obj/Debug/net9.0/generated/EastFive.Core.Generators/
    EastFive.Core.Generators.InvokeDelayedGenerator/
        DiscriminatedFunctions.InvokeDelayed.g.cs
```

You can inspect this file to see all generated overloads.

## Adding More Combinations

To add more combinations, modify the `combinations` array in `InvokeDelayedGenerator.cs`:

```csharp
var combinations = new[]
{
    (0, 1), (0, 2), (0, 3), (0, 4),  // 0 primes
    (1, 1), (1, 2), (1, 3), (1, 4),  // 1 prime
    (2, 1), (2, 2), (2, 3),          // 2 primes
    (3, 1), (3, 2), (3, 3), (3, 4),  // 3 primes
    (4, 1), (4, 2),                   // 4 primes (add this line)
};
```

Then rebuild the generator project.

## Performance Characteristics

All generated overloads use the same optimized pattern:
- ✅ Parallel execution of delayed callbacks
- ✅ Early termination on short-circuit
- ✅ Proper TaskCompletionSource coordination
- ✅ No blocking waits

This ensures consistent performance across all overload combinations.
