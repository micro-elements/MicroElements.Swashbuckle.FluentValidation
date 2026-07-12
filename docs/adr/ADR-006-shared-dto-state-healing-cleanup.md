# ADR-006: State-Healing Schema Cleanup for Shared [FromQuery] DTOs via SchemaRepository.ReplaceSchemaId

**Status:** Implemented
**Date:** 2026-07-12
**Issue:** [#226](https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/226) (follow-up to [#223](https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/223); approach suggested by @jgarciadelanoceda in [PR #224 discussion](https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/pull/224#issuecomment-4940080089))
**Milestone:** v7.1.11

---

## 1. Context and Problem

### Background

Since Issue #180 (see [ADR-002](ADR-002-configurable-schema-cleanup.md)), `FluentValidationOperationFilter.ApplyRulesToParameters` removes component schemas that were created only as a side effect of our own `GetSchemaForType` calls for `[FromQuery]`/`[AsParameters]` container types (Swashbuckle expands those into individual parameters and never references the container component). The cleanup is controlled by `SchemaGenerationOptions.RemoveUnusedQuerySchemas` (default `true`) and runs **once per operation** (`FluentValidationOperationFilter.cs:253-264`).

Issue #223 discovered that this cleanup leaves the `SchemaRepository` in a **reserved-but-removed** state: the schema is removed from `SchemaRepository.Schemas`, but Swashbuckle keeps the type in its internal `_reservedIds` map, which the cleanup cannot touch (no public API existed). In that state `ISchemaGenerator.GenerateSchema` returns a bare `$ref` (no `Properties`) without regenerating the component. PR #224 (v7.1.10) fixed the *parameters* path by recovering the concrete schema into a throwaway `SchemaRepository` (`SwashbuckleSchemaProvider.GetSchemaForType`, lines 69-86) — a read-only recovery that deliberately does not mutate the shared repository.

### Remaining Problem (Issue #226)

The throwaway recovery is only safe where the recovered schema is used for **reading** constraint values. Two gaps remain when the same DTO type is used **both** as a flattened `[FromQuery]`/`[AsParameters]` container **and** as a request body (`[FromBody]` or `[FromForm]`) in the same document, with `RemoveUnusedQuerySchemas = true` (the default) and the query operation processed **before** the body operation:

1. **`[FromBody]` (JSON): dangling `$ref` in the emitted document.** The operation filter's request-body path does not process `application/json` at all — its content-type gate only admits form content types (`FluentValidationOperationFilter.cs:416-422`), so JSON bodies are covered by the *schema filter* (`FluentValidationRules`) at component-generation time. But in the reserved-but-removed state, when Swashbuckle generates the body operation it calls `GenerateSchema` for the body type, hits the reservation, and emits a `$ref` to a component that **no longer exists** — the schema filter is never re-invoked because the component is not regenerated. The result is an invalid document (dangling reference) with no FluentValidation rules for the body.
   `NOTE: originally a hypothesis from reading Swashbuckle's SchemaRepository/SchemaGenerator source (the reserved-ids check precedes generation). CONFIRMED empirically on 2026-07-12 by the Phase 1 repro: SharedDtoMixedBindingTests fails pre-fix with SchemaRepository.Schemas empty while the type stays reserved — GenerateSchema returns a $ref to a component that does not exist.`

2. **`[FromForm]`: rules applied to a disposable object.** For form content types, `ApplyRulesToRequestBody` resolves the container schema via `schemaProvider.GetSchemaForType(parameterType)` when `schemaRefId != null` (`FluentValidationOperationFilter.cs:464`) and then passes it to `FluentValidationSchemaBuilder.ApplyRulesToSchema` as the **mutation target** (lines 480-485) — there is no copy-back onto a separate document object. In the reserved-but-removed state, `GetSchemaForType` returns the detached throwaway-recovered instance (the #224 fallback), so all applied rules are written to an object the document never sees and are silently lost.

### Why the #224 fix cannot close this

The #224 recovery is intentionally isolated: it never mutates the shared repository, so it cannot restore the missing component or clear the stale reservation. Fixing the body scenarios requires the shared `SchemaRepository` state itself to be consistent after cleanup.

### The new tool: `SchemaRepository.ReplaceSchemaId`

Swashbuckle.AspNetCore **10.1.0** added a public `SchemaRepository.ReplaceSchemaId(Type schemaType, string replacementSchemaId)` ([PR #3708](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/pull/3708)). It moves the schema to a new key in `Schemas` **and removes the type from the private `_reservedIds` map** — exactly the half of the state that public API could not reach before. It returns `false` when the schema is not present under the old id, so it must be called **before** the schema is removed.

### Version matrix

| TFM | Swashbuckle.AspNetCore.SwaggerGen | `ReplaceSchemaId` available |
|-----|-----------------------------------|------------------------------|
| net10.0 (`OPENAPI_V2`) | 10.2.1 | Yes (since 10.1.0) |
| net8.0, net9.0 | 8.1.1 | No |

Bumping the net8.0/net9.0 minimum to 10.1.0+ is not acceptable: Swashbuckle 9+ moves to Microsoft.OpenApi 2.x, a large breaking change for consumers.

## 2. Options Considered

### Option A: State-healing cleanup via `ReplaceSchemaId` on net10.0 (CHOSEN)

During `ApplyRulesToParameters`, track the container `Type` for every `GetSchemaForType` call made while `RemoveUnusedQuerySchemas` is active. In the Issue #180 cleanup, for each schema id that did not exist before the operation and whose `Type` is known, call `ReplaceSchemaId(type, tempId)` (which clears the reservation) and then remove the renamed entry. Ids without a tracked type (components for nested complex property types, created transitively by `GenerateSchema`) keep the current plain removal.

**Pros:**
- Fixes the root cause: the reserved-but-removed state never arises for container types, so subsequent operations (query, body, or response) regenerate a full component and the schema filter re-applies rules to a real document object.
- Closes both #226 scenarios on net10.0, including the dangling-`$ref` document-validity hazard.
- Public API only — no reflection, no behavior dependent on Swashbuckle internals.
- Removes the repeated throwaway regeneration cost noted in the PR #224 review (the recovered-state branch stops firing for tracked types).

**Cons:**
- net10.0-only (`OPENAPI_V2`); net8.0/net9.0 keep the #224 behavior (see Option B for why reflection was rejected).
- Components created transitively inside Swashbuckle's `GenerateSchema` (nested property types whose `Type` never passes through library code) still leave stale reservations; the #224 throwaway fallback must therefore stay as a safety net on all TFMs.
- Requires an operation-scoped `Type → schemaId` tracking map, best recorded inside `SwashbuckleSchemaProvider` itself so that **every** `GetSchemaForType` call site participates — including the #209 ancestor-requiredness walk (`IsPropertyRequiredInType`, `FluentValidationOperationFilter.cs:358`), which also registers ancestor container schemas that the cleanup removes.

### Option B: Reflection-based healing of `_reservedIds` on net8.0/net9.0

Mirror what the author of Swashbuckle issue #3707 did before the API existed: after removing the schema, also remove the type from the private `_reservedIds` dictionary via reflection.

**Pros:**
- Closes the gap uniformly on all TFMs.

**Cons:**
- Depends on a private field name and shape across all Swashbuckle 8.x patch versions the floating consumer graph may resolve; silently breaks (reverting to the current behavior at best) if the field changes.
- The maintainer explicitly rejected reflection for this fix (assumption list, 2026-07-12): net8.0/net9.0 stay on the #224 throwaway fallback.

**REJECTED** — fragility outweighs the benefit; the affected mixed-binding scenario is narrow and the #224 fallback keeps the primary #223 case fixed on those TFMs.

### Option C: Promote `FluentValidationDocumentFilter` (Variant B from #223)

Make the experimental document filter (`RegistrationOptions.ExperimentalUseDocumentFilter`, default `false`) production-ready and the default pipeline. It batches all types/parameters across the whole document and performs the #180 cleanup once at the end (`FluentValidationDocumentFilter.cs:253-265`), so per-operation desync cannot occur.

**Pros:**
- Structural fix for the whole class of per-operation state bugs, uniformly across TFMs.
- Scenario 1 (JSON dangling `$ref`) does not arise under the document filter even today: its cleanup runs once at the end of the document (`FluentValidationDocumentFilter.cs:253-265`), after Swashbuckle has registered body components (protected by the `existingSchemaIds` snapshot); its type-level loop (lines 92-111) collects `[FromBody]` component types, and the subsequent rules loop (lines 164-201) applies rules to those schemas.

**Cons:**
- The document filter never touches `operation.RequestBody`: scenario 2 (`[FromForm]`) and the #216 `encoding.contentType` parity would require substantial new code.
- Large parity checklist before promotion: required marking (#209), request bodies + `[FromForm]` + `encoding.contentType` (#216), `FindParam` inspects only the first operation of a path (`FluentValidationDocumentFilter.cs:152-162`), dead duplicate `schemasForParameters` assignment (lines 113-119 overwritten at 150).
- A default-pipeline swap is a behavior change requiring its own ADR and migration plan; it does not help the default (operation-filter) pipeline that all current consumers run.

**REJECTED for this ADR** — remains the long-term structural direction, tracked separately.

### Option D: Do nothing beyond #224

**Pros:**
- Zero risk; #223 (the reported, common case) is already fixed.

**Cons:**
- The dangling-`$ref` hazard and the `[FromForm]` rule loss remain reachable with default settings (`RemoveUnusedQuerySchemas = true`) whenever a DTO is shared between query and body bindings.

**REJECTED** — an emitted-document validity hazard with default settings is worth closing where the public API allows it.

## 3. Decision: Option A

Implement state-healing cleanup in `FluentValidationOperationFilter` on the net10.0 (`OPENAPI_V2`) target using `SchemaRepository.ReplaceSchemaId`, keeping the #224 throwaway recovery as a safety net for net8.0/net9.0 and for transitively created nested components.

**Rationale:**

1. Prevention beats compensation: with the reservation cleared, every downstream consumer of the type (our filter, Swashbuckle's own body/response generation, the schema filter) sees a normal repository and behaves correctly — no path-by-path patching.
2. Public API only: the fix cannot silently break on a Swashbuckle patch bump; on the one TFM where the API exists, we use it; where it does not, we do not degrade anything.
3. Small blast radius: the change is confined to the cleanup block and an operation-scoped tracking collection; the shared core, NSwag, and AspNetCore.OpenApi packages are untouched (their pipelines have no `SchemaRepository`/reserved-ids mechanic).

**Cleanup mechanics (net10.0):**

```csharp
// SwashbuckleSchemaProvider records Type -> schemaId for every GetSchemaForType call
// (covers the parameters loop AND the #209 ancestor walk in IsPropertyRequiredInType):
containerTypesForCleanup[type] = schemaId;

// Issue #180 cleanup, per newly created schemaId:
#if OPENAPI_V2
if (typeForSchemaId is not null)
{
    var tempId = "__fv_removed_" + Guid.NewGuid().ToString("N");
    if (context.SchemaRepository.ReplaceSchemaId(typeForSchemaId, tempId)) // clears the reservation
    {
        context.SchemaRepository.Schemas.Remove(tempId);
        continue;
    }
}
#endif
context.SchemaRepository.Schemas.Remove(schemaId); // fallback: current behavior (nested types, net8/net9)
```

On net8.0/net9.0 the cleanup body is unchanged; the #224 fallback in `SwashbuckleSchemaProvider.GetSchemaForType` continues to recover readable schemas for the parameters path.

## 4. Implementation

Affected package: **`MicroElements.Swashbuckle.FluentValidation` only.**

### Phase 1 — Reproduction tests (investigation)

New test file: `test/MicroElements.Swashbuckle.FluentValidation.Tests/SharedDtoMixedBindingTests.cs`.

1. `OPENAPI_V2` (net10.0) test: shared DTO bound as `[FromQuery]` on operation 1 and `[FromBody]` (JSON) on operation 2, one shared `SchemaRepository`/`SchemaGenerator`, `RemoveUnusedQuerySchemas = true`. Assert: after both operations, the component schema for the type **exists** in `SchemaRepository.Schemas` (no dangling `$ref`) and carries FluentValidation constraints. This test confirms (or refutes) the dangling-`$ref` hypothesis from Context — if refuted, the ADR scope shrinks to the `[FromForm]` scenario and the Context section must be corrected.
2. Same-shape test with `[FromForm]` (`multipart/form-data`): assert the request-body schema the document references carries the applied rules.
3. Port the #223 regression scenario to `OPENAPI_V2` — extend `test/MicroElements.Swashbuckle.FluentValidation.Tests/SharedDtoAcrossEndpointsTest.cs` with an `OPENAPI_V2`-compatible variant (its current test body is `#if !OPENAPI_V2`), so the net10.0 target where the chosen state-healing cleanup (Option A) applies gets shared-DTO coverage.

### Phase 2 — State-healing cleanup

4. `SwashbuckleSchemaProvider`: record an operation-scoped `Type → schemaId` entry for every `GetSchemaForType` call (the provider already computes the schema id per call). This covers **all** call sites — the parameters loop and the #209 ancestor-requiredness walk in `IsPropertyRequiredInType` (`FluentValidationOperationFilter.cs:358`). The #223 throwaway fallback stays unchanged as safety net.
5. `FluentValidationOperationFilter.ApplyRulesToParameters` cleanup block: under `#if OPENAPI_V2`, apply the `ReplaceSchemaId` + remove dance from Decision for every newly created id with a tracked `Type`; keep plain removal for untracked ids and non-`OPENAPI_V2` builds.

### Phase 3 — Verification

6. Full matrix run: net8.0/net9.0 (behavior unchanged — all existing tests green), net10.0 (new tests green, existing 96 green).
7. Manual check with `samples/SampleWebApi` (`Controllers/Issue80.cs` already binds the same `BestShot` type as `[FromBody]` and `[FromQuery]`, and already ships `Issue80.Validator : AbstractValidator<BestShot>` with `MinimumLength(5)` on `Link` — use it as-is to observe the constraints). Note: `SampleWebApi` targets net9.0, so it exercises the #224 throwaway fallback, not the net10.0 healing; the healing itself is covered by the `OPENAPI_V2` tests above.

## 5. Testing

- **New tests** (see Phase 1): mixed `[FromQuery]`+`[FromBody]`, mixed `[FromQuery]`+`[FromForm]`, and the `OPENAPI_V2` port of the #223 shared-DTO regression. All follow the `SharedDtoAcrossEndpointsTest` pattern: one shared `SchemaRepository`/`SchemaGenerator` driven through multiple operations, matching the real Swagger pipeline.
- **Why existing tests didn't catch this:** no test in the repository binds the same DTO type through two different binding sources in one document (confirmed by search, 2026-07-12); and the only shared-DTO test (`SharedDtoAcrossEndpointsTest`) is excluded on `OPENAPI_V2` — exactly the target where `ReplaceSchemaId` lives.
- **Sample:** `samples/SampleWebApi`, `Controllers/Issue80.cs` (`PostBestShot([FromBody] BestShot)` + `PostBestShot2([FromQuery] BestShot)`).

## 6. Consequences

### Positive

- On net10.0, the `SchemaRepository` is left consistent after every operation: no reserved-but-removed state, no dangling `$ref` in emitted documents, FluentValidation rules reach body-bound shared DTOs.
- The #224 review's performance note is addressed for tracked types (no repeated throwaway regeneration).
- No public API changes; no reflection; other packages untouched.

### Negative

- Behavior diverges by TFM: net10.0 is fully healed, net8.0/net9.0 keep the narrower #224 guarantees (parameters path only). This must be stated in the README compatibility notes.
- Components created transitively inside Swashbuckle's `GenerateSchema` still leave stale reservations on all TFMs (nested property types whose `Type` never passes through library code — everything that does pass through `GetSchemaForType`, including the #209 ancestor walk, is tracked and healed); the throwaway fallback continues to cover reads, but body-bound shared types of that transitive shape remain theoretically affected on net10.0 as well. `NOTE: no known report of this shape; revisit if one appears.`

### Risks

- If the Phase 1 `[FromBody]` repro refutes the dangling-`$ref` hypothesis (e.g. Swashbuckle regenerates the component in some path not covered by source reading), the Context section must be corrected and the ADR re-reviewed — the `[FromForm]` scenario and the state-healing rationale stand regardless.
- `ReplaceSchemaId` returns `false` when the target id already exists in `Schemas`; the GUID-based temp id makes a collision practically impossible, and the `false` branch safely degrades to the current plain removal.

### Semver impact

Patch release (**v7.1.11**): bug fix, no public API surface change, default behavior becomes strictly more correct.
