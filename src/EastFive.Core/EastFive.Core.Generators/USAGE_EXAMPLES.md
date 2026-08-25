# Example Usage of Generated InvokeDelayed Overloads

This document demonstrates the various `InvokeDelayed` overloads that are now available thanks to the source generator.

## Original Manual Overload

**2 Primes, 4 Delayed** (manually written in DiscriminatedFunctions.cs):
```csharp
Func<TPrime1, TPrime2, TDelayed1, TDelayed2, TDelayed3, TDelayed4, Task<TResult>>
  → Func<TPrime1, TPrime2, Task<TResult>>
```

## Generated Overloads

### 0 Primes (Execute without parameters)

**1 Delayed:**
```csharp
var processTask = new Func<IRef<Account>, Task<HttpResponseMessage>>(...);
var optimized = processTask.InvokeDelayed(
    async (onFound) => await accountRef.StorageGetAsync(onFound, () => NotFound())
);
var result = await optimized(); // No parameters needed!
```

**2 Delayed:**
```csharp
var processTask = new Func<IRef<Account>, IRef<Practice>, Task<HttpResponseMessage>>(...);
var optimized = processTask.InvokeDelayed(
    async (onFound) => await accountRef.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await practiceRef.StorageGetAsync(onFound, () => NotFound())
);
var result = await optimized(); // Fetches both in parallel!
```

### 1 Prime (Single parameter)

**1 Delayed:**
```csharp
var processTask = new Func<IRef<Department>, IRef<Account>, Task<HttpResponseMessage>>(...);
var optimized = processTask.InvokeDelayed(
    async (onFound) => await accountRef.StorageGetAsync(onFound, () => NotFound())
);
var result = await optimized(departmentRef); // One param, one delayed
```

**3 Delayed:**
```csharp
var processTask = new Func<IRef<Department>, IRef<Account>, IRef<Practice>, IRef<Patient>, Task<HttpResponseMessage>>(...);
var optimized = processTask.InvokeDelayed(
    async (onFound) => await accountRef.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await practiceRef.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await patientRef.StorageGetAsync(onFound, () => NotFound())
);
var result = await optimized(departmentRef); // One param, three delayed fetches in parallel!
```

### 2 Primes (Two parameters)

**1 Delayed:**
```csharp
var processTask = new Func<IRef<Account>, IRef<Practice>, IRef<Department>, Task<HttpResponseMessage>>(...);
var optimized = processTask.InvokeDelayed(
    async (onFound) => await departmentRef.StorageGetAsync(onFound, () => NotFound())
);
var result = await optimized(accountRef, practiceRef); // Two params, one delayed
```

**2 Delayed:**
```csharp
var processTask = new Func<IRef<Practice>, IRef<Patient>, IRef<Account>, IRef<Department>, Task<HttpResponseMessage>>(...);
var optimized = processTask.InvokeDelayed(
    async (onFound) => await accountRef.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await departmentRef.StorageGetAsync(onFound, () => NotFound())
);
var result = await optimized(practiceRef, patientRef); // Two params, two delayed in parallel
```

### 3 Primes (Three parameters)

**2 Delayed:**
```csharp
var processTask = new Func<IRef<Account>, IRef<Practice>, IRef<Department>, IRef<Patient>, IRef<Provider>, Task<HttpResponseMessage>>(...);
var optimized = processTask.InvokeDelayed(
    async (onFound) => await patientRef.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await providerRef.StorageGetAsync(onFound, () => NotFound())
);
var result = await optimized(accountRef, practiceRef, departmentRef); // Three params, two delayed
```

## Real-World Example from ACP Chat

Before (sequential execution):
```csharp
await chatAgent.StorageGetAsync(async (agent) =>
    await account.StorageGetAsync(async (acct) =>
        await practice.StorageGetAsync(async (prac) =>
            await script.StorageGetAsync(async (scr) =>
                // Finally do something with all four
            )
        )
    )
);
```

After (parallel execution):
```csharp
var processRequest = new Func<IRef<ChatAgent>, IRef<Account>, IRef<Practice>, IRef<AIScript>, Task<HttpResponseMessage>>(...);

var optimized = processRequest.InvokeDelayed(
    async (onFound) => await chatAgent.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await account.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await practice.StorageGetAsync(onFound, () => NotFound()),
    async (onFound) => await script.StorageGetAsync(onFound, () => NotFound())
);

var result = await optimized(); // All 4 storage gets happen in parallel!
```

## Performance Benefits

1. **Parallel Execution**: All delayed callbacks start simultaneously
2. **Early Termination**: If any callback short-circuits (returns NotFound), the entire operation terminates immediately
3. **Type Safety**: Full IntelliSense support for all generated overloads
4. **Zero Overhead**: Generated at compile-time, no runtime reflection

## Total Combinations Available

The generator currently produces **15 overloads**:
- 0 primes: 4 overloads (1-4 delayed)
- 1 prime: 4 overloads (1-4 delayed)
- 2 primes: 3 overloads (1-3 delayed) *2-4 manually written*
- 3 primes: 4 overloads (1-4 delayed)

This covers most common scenarios while keeping the generated code manageable.
