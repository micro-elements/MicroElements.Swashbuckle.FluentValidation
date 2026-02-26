# ADR-001: Microsoft.AspNetCore.OpenApi Support (IOpenApiSchemaTransformer)

**Status:** Accepted
**Date:** 2026-02-23
**Issue:** [#149](https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/149)
**Milestone:** v7.1.0

---

## 1. Context and Problem

Starting with .NET 9, Microsoft provides built-in OpenAPI support (`Microsoft.AspNetCore.OpenApi`):
- `builder.Services.AddOpenApi()` + `app.MapOpenApi()`
- Transformers: `IOpenApiSchemaTransformer`, `IOpenApiDocumentTransformer`, `IOpenApiOperationTransformer`

Users are migrating from Swashbuckle to the built-in solution. Our library must support both approaches.

**Neither .NET 9, nor .NET 10, nor future .NET versions include FluentValidation-to-OpenAPI mapping out of the box.** Microsoft provides only the transformer infrastructure, not the FluentValidation integration. Our library is needed for both versions.

### Reference Implementation (saithis)

User [saithis](https://github.com/saithis/dotnet-playground/tree/main/OpenApiFluentValidationApi) created a proof-of-concept:
- ~200 lines, standalone `FluentValidationSchemaTransformer : IOpenApiSchemaTransformer`
- Supports: NotNull, NotEmpty, Length, MinLength, MaxLength, Between, Comparison, Regex, Email, CreditCard
- Does NOT support: nested validators (SetValidator), Include(), RuleForEach(), When/Unless, AllOf, caching, rule customization

### .NET 9 vs .NET 10 Differences

| Aspect | .NET 9 | .NET 10 |
|--------|--------|---------|
| `IOpenApiSchemaTransformer` | Available | Available |
| `GetOrCreateSchemaAsync()` | No | Yes |
| `context.Document` | No | Yes |
| Microsoft.OpenApi version | v1.x | v2.x (breaking API) |
| `OPENAPI_V2` required | No | Yes |

Our library is needed for both versions. The differences are only in the `OpenApiSchema` model API.

---

## 2. Options Considered

### Option A: New Separate Package (CHOSEN)

```
MicroElements.OpenApi.FluentValidation          (core, generic abstractions)
    ^                      ^
    |                      |
Swashbuckle package    NEW: AspNetCore.OpenApi package
(ISchemaFilter)        (IOpenApiSchemaTransformer)
```

- `MicroElements.AspNetCore.OpenApi.FluentValidation`
- Targets: `net9.0;net10.0`
- Dependencies: core + `Microsoft.AspNetCore.OpenApi` (NO Swashbuckle)
- Duplicates ~630 lines of OpenApiSchema-specific code
- Extraction to shared layer planned for Phase 2 (v7.2)

### Option B: Extract Shared OpenApi Layer (deferred to v7.2)

```
MicroElements.OpenApi.FluentValidation           (core, generic)
    ^
MicroElements.OpenApi.FluentValidation.Rules      (NEW: shared OpenApiSchema rules)
    ^                      ^
Swashbuckle package     NEW: AspNetCore.OpenApi package
```

- Extracts shared code into a shared package
- `[TypeForwardedTo]` for compatibility
- Zero duplication, but more complex with risk of breaking changes

### Option C: Minimal Integration (rejected)

- net9.0 only, no OPENAPI_V2
- Maximum duplication, no net10.0

---

## 3. Decision: Option A (Phased)

**Phase 1 (v7.1.0):** New package with controlled duplication
**Phase 2 (v7.2):** Extract shared layer, clean up namespaces

### Rationale
- Fast release without breaking changes for existing users
- Duplication is manageable (~630 lines, well-defined set of files)
- Follows the NSwag package precedent
- Shared layer extraction planned for v7.2

---

## 4. New Package Architecture

### 4.1 Dependency Graph

```
MicroElements.AspNetCore.OpenApi.FluentValidation
  -> MicroElements.OpenApi.FluentValidation (core)
       -> FluentValidation >= 12.0.0
       -> Microsoft.Extensions.Logging.Abstractions
       -> Microsoft.Extensions.Options
  -> Microsoft.AspNetCore.OpenApi (>= 9.0.0 for net9.0, >= 10.0.0 for net10.0)
  [NO dependency on Swashbuckle]
```

### 4.2 File Structure

```
src/MicroElements.AspNetCore.OpenApi.FluentValidation/
|
├── MicroElements.AspNetCore.OpenApi.FluentValidation.csproj
├── GlobalUsings.cs
|
├── FluentValidationSchemaTransformer.cs       # NEW: IOpenApiSchemaTransformer
├── AspNetCoreSchemaGenerationContext.cs        # NEW: ISchemaGenerationContext<OpenApiSchema>
├── AspNetCoreSchemaProvider.cs                 # NEW: ISchemaProvider<OpenApiSchema>
|
├── FluentValidationRule.cs                     # COPY from Swashbuckle
├── DefaultFluentValidationRuleProvider.cs      # COPY from Swashbuckle
├── OpenApiRuleContext.cs                       # COPY from Swashbuckle
|
├── OpenApi/
│   ├── OpenApiSchemaCompatibility.cs           # COPY from Swashbuckle
│   └── OpenApiExtensions.cs                    # COPY from Swashbuckle
|
├── Generation/
│   └── SystemTextJsonNameResolver.cs           # COPY from Swashbuckle
|
└── AspNetCore/
    ├── AspNetJsonSerializerOptions.cs          # COPY from Swashbuckle
    ├── ReflectionDependencyInjectionExtensions.cs # COPY from Swashbuckle
    ├── ServiceCollectionExtensions.cs          # NEW: DI registration
    └── OpenApiOptionsExtensions.cs             # NEW: AddFluentValidationRules()
```

### 4.3 File Classification

| File | Type | Source |
|------|------|--------|
| `.csproj` | New | - |
| `GlobalUsings.cs` | Copy | Swashbuckle GlobalUsings.cs |
| `FluentValidationSchemaTransformer.cs` | **New** | Based on FluentValidationRules.cs pattern |
| `AspNetCoreSchemaGenerationContext.cs` | **New** | Based on SchemaGenerationContext.cs pattern |
| `AspNetCoreSchemaProvider.cs` | **New** | net9: stub, net10: GetOrCreateSchemaAsync |
| `FluentValidationRule.cs` | Copy | Swashbuckle FluentValidationRule.cs |
| `DefaultFluentValidationRuleProvider.cs` | Copy | Swashbuckle DefaultFluentValidationRuleProvider.cs |
| `OpenApiRuleContext.cs` | Copy | Swashbuckle OpenApiRuleContext.cs |
| `OpenApiSchemaCompatibility.cs` | Copy | Swashbuckle OpenApiSchemaCompatibility.cs |
| `OpenApiExtensions.cs` | Copy | Swashbuckle OpenApiExtensions.cs |
| `SystemTextJsonNameResolver.cs` | Copy | Swashbuckle SystemTextJsonNameResolver.cs |
| `AspNetJsonSerializerOptions.cs` | Copy | Swashbuckle AspNetJsonSerializerOptions.cs |
| `ReflectionDependencyInjectionExtensions.cs` | Copy | Swashbuckle ReflectionDependencyInjectionExtensions.cs |
| `ServiceCollectionExtensions.cs` | **New** | Based on Swashbuckle ServiceCollectionExtensions.cs pattern |
| `OpenApiOptionsExtensions.cs` | **New** | - |

---

## 5. User-Facing API

### Registration in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register FluentValidation validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Register FluentValidation OpenAPI support
builder.Services.AddFluentValidationRulesToOpenApi();

// Add OpenApi with the FluentValidation transformer
builder.Services.AddOpenApi(options =>
{
    options.AddFluentValidationRules();
});

var app = builder.Build();
app.MapOpenApi();
app.Run();
```

### Migration from Swashbuckle

```diff
// NuGet
- MicroElements.Swashbuckle.FluentValidation
+ MicroElements.AspNetCore.OpenApi.FluentValidation

// Program.cs
- services.AddSwaggerGen();
- services.AddFluentValidationRulesToSwagger();
+ services.AddFluentValidationRulesToOpenApi();
+ services.AddOpenApi(options => options.AddFluentValidationRules());

// Namespace
- using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
+ using MicroElements.AspNetCore.OpenApi.FluentValidation;
```

---

## 6. Known Limitations

1. **Nested validators on .NET 9**: `SetValidator<T>()` sub-schema resolution is limited (no `GetOrCreateSchemaAsync`). Full support on .NET 10.
2. **Transformer granularity**: `IOpenApiSchemaTransformer` is called per-schema (including property schemas). Must filter by `context.JsonPropertyInfo == null`.
3. **Code duplication**: ~630 lines duplicated from the Swashbuckle package. Bug fixes must be applied in both places until v7.2 (Phase 2).

---

## 7. Verification

### 7.1 Build
```bash
dotnet build MicroElements.Swashbuckle.FluentValidation.sln
```
- All projects compile without errors

### 7.2 Tests
```bash
dotnet test MicroElements.Swashbuckle.FluentValidation.sln
```
- Existing tests pass (no regressions)
- New tests for all rule types pass

### 7.3 Sample Application
```bash
cd samples/SampleAspNetCoreOpenApi
dotnet run
# Open /openapi/v1.json
```
- OpenAPI document contains validation constraints

### 7.4 Dependencies
- NO transitive dependency on Swashbuckle
- Depends on MicroElements.OpenApi.FluentValidation and Microsoft.AspNetCore.OpenApi
