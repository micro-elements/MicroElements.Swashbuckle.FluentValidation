# ADR-002: Configurable Schema Cleanup for Query Parameter Container Types

**Status:** Accepted
**Date:** 2026-02-26
**Issue:** [#180 (comment)](https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/180#issuecomment-3968741044)
**Milestone:** v7.0.5

---

## 1. Context and Problem

### Background

In v7.0.4, a fix for issue #180 removed side-effect schemas from `components/schemas` created when the library processes `[FromQuery]`/`[AsParameters]` container types. `SwashbuckleSchemaProvider.GetSchemaForType()` calls `GenerateSchema()` which registers schemas in `SchemaRepository` as a side-effect. Since Swashbuckle expands these types into individual query parameters (not `$ref` schemas), the registered schemas are unreferenced and pollute the OpenAPI document.

The fix takes a snapshot of existing schema IDs before processing and removes any newly-created schemas afterward. This exists in:

- `FluentValidationOperationFilter.ApplyRulesToParameters()`
- `FluentValidationDocumentFilter.Apply()`

### The Regression

User @thunderstatic reports v7.0.4 breaks a workflow that worked since ~5.7.0:

1. Custom FluentValidation rules (e.g. `IsOneOfString`) constrain `string[]` properties
2. MicroElements writes `enum` on the schema in `components/schemas`
3. A custom `IDocumentFilter` copies enum values from `components/schemas` onto query parameters
4. With v7.0.4, source schemas are removed before the custom DocumentFilter runs

### Tension

Two legitimate, conflicting needs:
- **Original #180 reporters**: clean `components/schemas` without unreferenced container type schemas
- **@thunderstatic's workflow**: schemas must remain for downstream DocumentFilters to consume

---

## 2. Options Considered

### Option A: Boolean Flag on `SchemaGenerationOptions` (CHOSEN)

Add `RemoveUnusedQuerySchemas` property (default: `true`) to the core `ISchemaGenerationOptions`/`SchemaGenerationOptions`.

**Pros:** Simple, consistent with existing patterns, accessible from both filters
**Cons:** Technically Swashbuckle-specific concern in core package

### Option B: Flag on `RegistrationOptions` (Swashbuckle-specific)

Add to `RegistrationOptions` which is only consumed at DI registration.

**Pros:** Correct layer placement
**Cons:** `RegistrationOptions` is not injected into filters; requires significant plumbing to pass through

### Option C: Predicate/Callback for Per-Schema Decisions

`Func<string, OpenApiSchema, bool>? ShouldRemoveSchema`

**Pros:** Maximum flexibility
**Cons:** Over-engineered; no user has requested per-schema granularity

### Option D: Reference-Counting (Remove Only Truly Unreferenced)

Scan entire document post-processing, remove only schemas with zero `$ref` references.

**Pros:** Semantically correct
**Cons:** Does not solve the problem (user's DocumentFilter has not run yet, so schemas appear unreferenced); expensive; fragile

---

## 3. Decision: Option A

**Rationale:**
1. A single boolean with sensible default is simplest to implement, document, and use
2. `SchemaGenerationOptions` is already the single options class both filters read; alternatives add plumbing for no benefit
3. `SchemaGenerationOptions` already contains Swashbuckle-leaning properties (e.g. `SchemaIdSelector`)
4. Default `true` preserves v7.0.4 behavior; opt-in `false` restores compatibility
5. NSwag and AspNetCore.OpenApi packages simply ignore the property

**User-facing API:**
```csharp
services.AddFluentValidationRulesToSwagger(options =>
{
    options.RemoveUnusedQuerySchemas = false;
});
```

---

## 4. Implementation

### Changes Made

1. **`ISchemaGenerationOptions`** - Added `bool RemoveUnusedQuerySchemas` property
2. **`SchemaGenerationOptions`** - Added implementation with default `true`
3. **`SchemaGenerationOptionsExtensions.SetFrom()`** - Copies the new property
4. **`FluentValidationOperationFilter.ApplyRulesToParameters()`** - Snapshot and cleanup are conditional on the flag
5. **`FluentValidationDocumentFilter.Apply()`** - Snapshot and cleanup are conditional on the flag
6. **Tests** - Added `Default_RemoveUnusedQuerySchemas_Should_Be_True` and `OperationFilter_Should_Preserve_Schemas_When_RemoveUnusedQuerySchemas_Is_False`

---

## 5. Consequences

### Positive
- Users who depend on container type schemas for custom DocumentFilters can opt out of cleanup
- Default behavior remains clean (v7.0.4 fix preserved)
- Minimal implementation complexity

### Negative
- Swashbuckle-specific concern leaks into the core `ISchemaGenerationOptions` interface
- NSwag/AspNetCore.OpenApi packages expose a property they don't use

### Risks
- None significant; the property is additive and backward-compatible
