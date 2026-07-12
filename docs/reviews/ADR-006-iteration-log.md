# ADR-006 Iteration Log

ADR: `docs/adr/ADR-006-shared-dto-state-healing-cleanup.md`
Driven by: `/adr-autonomous` (issue #226)

## Iteration 0 — Recon + assumptions (2026-07-12)

- Step −1 (Explore recon) findings that shaped the draft:
  - `ApplyRulesToRequestBody` skips `application/json` entirely (content-type gate `FluentValidationOperationFilter.cs:416-422`) — JSON bodies are owned by the schema filter, so the #226 `[FromBody]` failure mode is a **dangling `$ref`** (component removed, reservation prevents regeneration), not a lost in-place mutation.
  - `[FromForm]` path mutates `resolvedSchema` in place with no copy-back (`FluentValidationOperationFilter.cs:464,480-485`) — throwaway-recovered instance loses rules.
  - `FluentValidationDocumentFilter` (Variant B) does not process request bodies at all today → cannot close #226 as-is.
  - No mixed `[FromQuery]`+`[FromBody]` test exists; `SharedDtoAcrossEndpointsTest` is `#if !OPENAPI_V2` (no net10.0 shared-DTO coverage).
  - Parity: NSwag / AspNetCore.OpenApi pipelines have no `SchemaRepository`/reserved-ids — Swashbuckle-package-local change.
  - Repro seed: `samples/SampleWebApi/Controllers/Issue80.cs` (`BestShot` bound as both `[FromBody]` and `[FromQuery]`).
- Step 0 assumptions confirmed by user (2026-07-12): Swashbuckle package only; Variant C on net10.0/`OPENAPI_V2` only; **no reflection** on net8/net9; milestone **v7.1.11** (patch); out of scope: Variant B promotion, Swashbuckle minimum bump, NSwag/AspNetCore.OpenApi parity.

## Iteration 1 — Draft v1 (2026-07-12)

- Wrote ADR-006 v1: Context (two failure modes, dangling-`$ref` marked as hypothesis requiring Phase 1 repro), 4 options (A CHOSEN: `ReplaceSchemaId` state-healing on net10.0; B reflection REJECTED per user; C Variant B REJECTED for this ADR; D do-nothing REJECTED), Decision with cleanup mechanics sketch, 3-phase Implementation (repro tests → healing → matrix verification), Testing (why existing tests missed it), Consequences (TFM divergence, nested-type residual gap, semver patch).
- `/review-adr` iteration 1 result: **blocker 0, important 1, nit 2** (raw 4 findings from 3 validators; 1 duplicate deduped). Validator 1 (dotnet-architect) returned zero findings and independently corroborated `ReplaceSchemaId` presence by decompiling cached Swashbuckle DLLs (present in 10.1.5/10.2.1, absent in 10.0.0/8.1.1).

## Iteration 2 — Revise after review 1 (2026-07-12)

- Fixed **important** (Validator 2, rubric 5): Option C's first con overstated the gap — the document filter's end-of-document cleanup already prevents scenario 1 (JSON dangling `$ref`), and its type-level loop covers `[FromBody]` component schemas. Moved that fact to Option C **Pros**, narrowed the con to `operation.RequestBody`/`[FromForm]`/#216 encoding.
- Fixed **nit** (V2+V3, deduped): `Issue80.Validator : AbstractValidator<BestShot>` already exists in the sample — Phase 3 item 7 now says "use it as-is" instead of "add a validator". (Applied inline instead of deferring — trivial.)
- Fixed **nit** (V3): named the planned test artifacts — new `SharedDtoMixedBindingTests.cs`, #223 `OPENAPI_V2` port via extending `SharedDtoAcrossEndpointsTest.cs`. (Applied inline instead of deferring — trivial.)
- `/review-adr` iteration 2 result: **blocker 0, important 0, nit 3** → decision gate PASSED. Validator 2 re-verified the corrected Option C wording against `FluentValidationDocumentFilter.cs` (type selection 92-96/105-111, rules loop 164-201, snapshot 85-87, cleanup 253-265) — holds.

## Iteration 2 findings (all nit, applied inline — trivial and they sharpen implementation guidance)

- **[V1, rubric 1]** Option C Pros cited lines 92-111 for "applies rules", but that range only collects types; rule application is at 164-201. → Fixed: both ranges cited with correct verbs.
- **[V2, rubric 5]** Tracking scope was ambiguous: the #209 ancestor walk (`IsPropertyRequiredInType`, `FluentValidationOperationFilter.cs:358`) also calls `GetSchemaForType` with a fully known `Type`, so tracking only the parameters-loop call site would leave healable reservations stale. → Fixed: tracking moved into `SwashbuckleSchemaProvider` (covers every call site); Decision sketch, Phase 2 steps 4-5, Option A cons, and the Consequences-Negative bullet narrowed to "types that never pass through library code".
- **[V3, rubric 7]** "Variant C" used in Phase 1 item 3 but never defined in the ADR (its own Option C is the rejected document filter). → Fixed: replaced with "the chosen state-healing cleanup (Option A)".

## Exit (2026-07-12)

- Iterations used: **2 / 5**. Status: **PASS** (blocker 0, important 0 at iteration 2; the 3 iteration-2 nits were applied inline after the gate).
- Deferred: none.
- Next step: `/adr-implement` for `docs/adr/ADR-006-shared-dto-state-healing-cleanup.md` (Phase 1 repro tests first — they confirm or refute the marked dangling-`$ref` hypothesis).
