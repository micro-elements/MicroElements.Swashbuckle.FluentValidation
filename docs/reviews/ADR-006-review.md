# ADR-006 Review (2026-07-12)

**Final verdict (iteration 2): Approve** — blocker 0, important 0; 3 nits found in iteration 2 were applied inline (see `ADR-006-iteration-log.md`).

---

## Iteration 2 — re-validation after fixes

- Validator 1 (dotnet-architect): 1 nit — Option C Pros cited lines 92-111 for rule application (collection only; rules applied at 164-201). Fixed.
- Validator 2 (domain & parity): 1 nit — tracking scope ambiguous vs the #209 ancestor walk (`IsPropertyRequiredInType:358` also calls `GetSchemaForType` with a known `Type`). Fixed: tracking moved into `SwashbuckleSchemaProvider`, covering every call site. Re-verified the corrected Option C wording against code — holds.
- Validator 3 (structure): 1 nit — "Variant C" undefined inside the ADR. Fixed: replaced with in-ADR terminology (Option A).

---

# Iteration 1 (initial review)

ADR: `docs/adr/ADR-006-shared-dto-state-healing-cleanup.md`
Validators: 1 — `dotnet-artisan:dotnet-architect` (architecture/versions/semver), 2 — general-purpose (domain & parity), 3 — general-purpose (structure & completeness).

## Rubric summary

| # | Rubric item | Verdict |
|---|-------------|---------|
| 1 | Architecture & trade-offs (≥2 options, CHOSEN, layering) | ✅ 4 options, layering respected, core untouched |
| 2 | Generator parity | ✅ Swashbuckle-only justified; NSwag/AspNetCore.OpenApi verified to have no SchemaRepository mechanic |
| 3 | TFM/version matrix | ✅ All claims verified against code; `ReplaceSchemaId` corroborated by DLL decompilation (10.1.5/10.2.1 have it, 10.0.0/8.1.1 do not); the one hypothesis (dangling `$ref`) properly marked `requires investigation` |
| 4 | Public API & semver | ✅ No API change, patch v7.1.11 consistent |
| 5 | OpenAPI semantics | ⚠️ 1 important: Option C con overstated (document filter does cover JSON `[FromBody]` via type-level loop; scenario 1 doesn't arise under it) — **fixed in iteration 2** |
| 6 | Testing | ⚠️ 2 nits: stale "add a validator" premise (Issue80.Validator exists); planned tests not named — **both fixed in iteration 2** |
| 7 | Structure & format | ✅ English, header block, ADR-002/003 section style, REJECTED markers |

## 🔴 Blocker (0)

—

## 🟡 Important (1)

1. **[Validator 2, rubric 5, Section 2 Option C]** "The document filter does not process request bodies at all — would not fix #226 without substantial new code" overstates: `FluentValidationDocumentFilter`'s type-level loop (lines 92-111) covers `[FromBody]` component schemas and its end-of-document cleanup (253-265) prevents scenario 1; only scenario 2 (`[FromForm]`/encoding) needs substantial new code. → **Fixed**: fact moved to Option C Pros, con narrowed to `operation.RequestBody`/`[FromForm]`/#216.

## 🟢 Nit (2)

1. **[V2+V3 dedup, Phase 3 item 7]** `Issue80.Validator : AbstractValidator<BestShot>` (MinimumLength(5) on Link) already exists in the sample; "add a BestShot validator" was a stale premise. → **Fixed** ("use it as-is").
2. **[V3, Phase 1 / Testing]** Planned tests had no file/class names, unlike ADR-002/003 precedent. → **Fixed**: `SharedDtoMixedBindingTests.cs` (new), #223 `OPENAPI_V2` port via `SharedDtoAcrossEndpointsTest.cs`.

## Recommendation

**Approve с правками** (правки применены в iteration 2; повторная валидация — см. iteration log).

## Validator statistics

- Raw findings: V1 = 0, V2 = 2 (1 important, 1 nit), V3 = 2 (2 nits) → total 4.
- After dedup (`rubric_item`+`location`): 3 (1 important, 2 nits) — the Issue80-validator nit was reported by both V2 and V3.
