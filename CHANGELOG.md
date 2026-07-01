# Changes in 7.1.8
- Security (Issue #220): closed a transitive high-severity advisory in the published package. `Swashbuckle.AspNetCore.SwaggerGen` on the net10.0 target was bumped `10.0.0` → `10.2.1`, which resolves `Microsoft.OpenApi` to the patched `2.7.5` (was `2.3.0`). This clears **GHSA-v5pm-xwqc-g5wc / CVE-2026-49451** (CWE-674 uncontrolled recursion — a circular `$ref` schema could stack-overflow the OpenAPI reader). The net8.0/net9.0 targets use Swashbuckle 8.1.1 → Microsoft.OpenApi v1 and were never in the advisory range
- Media type & file size validation for `IFormFile` uploads (Issue #216): stable rollup of everything in `7.1.8-beta.1` and `7.1.8-beta.2` below (new File-level rules `.FileContentType()`, `.MaxFileSize()`, `.MinFileSize()`, `.FileSizeBetween()`; Swashbuckle / NSwag / Microsoft.AspNetCore.OpenApi emit multipart `encoding.contentType` and description annotations)

# Changes in 7.1.8-beta.2
- All of `7.1.8-beta.1` below, plus: **Microsoft.AspNetCore.OpenApi** now also emits `encoding.contentType` for the file part (Issue #216) — the `FluentValidationOperationTransformer` writes `requestBody.content["multipart/form-data"].encoding.<part>.contentType` so UIs like Scalar/Swagger UI can show the accepted media types, not just the description. Works on net9.0 (inline form schema) and net10.0 (resolves the whole-body `$ref` component to find the part name)

# Changes in 7.1.8-beta.1
- Added: media type (content type) and file size validation for `IFormFile` uploads (Issue #216)
  - New File-level FluentValidation rules in `MicroElements.OpenApi.FluentValidation` (namespace `MicroElements.OpenApi.FluentValidation.FileUpload`): `.FileContentType(params string[])`, `.MaxFileSize(long)`, `.MinFileSize(long)`, `.FileSizeBetween(long, long)` on `IRuleBuilder<T, IFormFile>`. They both enforce validation at runtime and surface metadata for OpenAPI generation
  - Root cause: rules on nested `IFormFile` members (`RuleFor(x => x.File.Length)` / `RuleFor(x => x.File.ContentType)`) are named `File.Length` / `File.ContentType` and never match the flat schema property `File`, so they were silently dropped; and `Must(...)` is opaque so allowed content types could not be reflected. Use the new File-level rules instead
  - **Swashbuckle**: emits `requestBody.content["multipart/form-data"].encoding.<part>.contentType` (comma-joined allowed types) and appends the allowed types and size limits to the file property `description`. File size is never emitted as `maxLength` (which counts characters, not bytes). Works on net8.0/net9.0 (Microsoft.OpenApi v1, OpenAPI 3.0) and net10.0 (Microsoft.OpenApi v2, OpenAPI 3.1)
  - **NSwag**: a new `FluentValidationOperationProcessor` (`IOperationProcessor`) emits multipart encoding for file parts; the allowed types and size limits are also appended to the file part `description`. Register it alongside the schema processor: `settings.OperationProcessors.Add(serviceProvider.GetService<FluentValidationOperationProcessor>())`. Known NSwag limitation: `OpenApiEncoding.EncodingType` serializes as `encodingType` rather than the OpenAPI-spec `contentType` (through at least NSwag 14.7.x), so the `description` is the guaranteed-visible carrier
  - **Microsoft.AspNetCore.OpenApi**: the allowed types and size limits are appended to the file property `description`, and (since `7.1.8-beta.2`) the allowed types are also emitted as `encoding.contentType` on the multipart media type
  - Purely additive / opt-in: behavior only changes when the new rules are used; no existing document output changes
  - File size has no standard OpenAPI/JSON-Schema byte keyword, so it is documented in the `description` (annotation only; enforcement stays server-side via FluentValidation)

# Changes in 7.1.7
- Fixed: The nested `[FromQuery]` fixes (#209 + #211) now also apply to the native `Microsoft.AspNetCore.OpenApi` transformer and the experimental Swashbuckle DocumentFilter (Issue #213)
  - `FluentValidationOperationTransformer` (package `MicroElements.AspNetCore.OpenApi.FluentValidation`) previously set a nested parameter `required` from the leaf validator alone — ignoring both whether the `SetValidator`/`ChildRules` chain reaches the leaf (#211) and whether every ancestor of the dot-path is required (#209). It now follows the same reachability + ancestor-required rules as the Swashbuckle `OperationFilter`
  - The experimental `FluentValidationDocumentFilter` no longer copies value constraints onto a flattened nested parameter whose nested validation is not wired from the root validator (#211)
  - `GetMethodInfo` now resolves the action method from `ControllerActionDescriptor` (MVC controllers), not only minimal-API endpoint metadata, so the dot-path root type can be resolved for controller actions
  - NSwag is unaffected (it has no `[FromQuery]` parameter flattening)
- Fixed: A validator for a nested type bound via `[FromQuery]` was reflected in the OpenAPI document even when it was **not** wired into the root validator via `SetValidator`/`ChildRules` (Issue #211)
  - `FluentValidationOperationFilter` resolved the leaf container's validator directly from the registry (by `ModelMetadata.ContainerType`), so a nested `NotEmpty()` marked the flattened parameter (e.g. `RequiredSubType.SubProperty`) as `required` even though FluentValidation never validates an unwired child object — the OpenAPI doc claimed `required`, but the API accepted requests without it
  - Fix: for a flattened nested parameter, nested rules are now applied only when the `SetValidator`/`ChildRules` chain from the action's root `[FromQuery]` validator actually reaches the leaf container; otherwise the parameter is left unconstrained, matching runtime behavior
  - When the root container type cannot be resolved, prior behavior is preserved (no regression for existing nested-parameter scenarios)
  - Behavioral change: when no validator is registered for the root `[FromQuery]` type (only a leaf/child validator is registered), a flattened nested parameter is now left unconstrained — matching runtime, where no validation runs without a root validator
- Fixed: A required leaf property inside an **optional** nested type bound via `[FromQuery]` was wrongly marked as a required parameter (Issue #209)
  - The 7.1.1 fix (Issue #162) made nested `[FromQuery]` validation match the leaf property name, but `FluentValidationOperationFilter` then set `required` based solely on the leaf type, ignoring whether the ancestor segment of the dot-path was optional
  - Because two nested properties of the same leaf type share one schema/validator (e.g. `OptionalSubType.SubProperty` and `RequiredSubType.SubProperty`), a `NotEmpty()` on the leaf marked **both** flattened parameters as required
  - Fix: a flattened nested parameter is now marked `required` only when **every** ancestor segment of the dot-path is required — resolved from the action's root `[FromQuery]` type, combining the native schema `required` (e.g. the C# `required` modifier) with FluentValidation `NotNull`/`NotEmpty` rules
  - Value constraints (e.g. `minLength`) still apply to an optional nested parameter when it is provided
  - When the root container type cannot be resolved, prior behavior is preserved (no regression for existing nested-parameter scenarios)

# Changes in 7.1.6
- Fixed: `$ref` still replaced with an inline copy (and the child component left orphaned) when nested object constraints come from `ChildRules` or an inline child validator (Issue #198, comment 4601720562)
  - The 7.1.3 fix restored unmodified `$ref`s, but when the nested type had no standalone validator its component schema gained its `Required` only after the parent's inline snapshot, so the stale `Required` diverged and defeated the restore check — leaving an inline copy and an orphaned component
  - Fix: the `Required` comparison in `HasValidationConstraintChanges` is now directional — restoration is only blocked when the inline copy carries a required entry the component lacks
  - `SetValidator` (with a standalone child validator) was already correct; `BigInteger`/enum per-model constraints (Issues #146/#176) continue to work
- Added: `ConditionalRulesMode` option to control how `.When()`/`.Unless()` conditional rules are handled during schema generation (Issue #203)
  - `Exclude` (default): conditional rules are excluded from the schema (backward-compatible, existing behavior)
  - `Include`: conditional rules are included in the schema (useful when `.When()` is a null-guard and constraints should still appear)
  - `IncludeWithWarning`: same as `Include` but logs a warning for each conditional rule included
  - Usage: `options.ConditionalRules = ConditionalRulesMode.Include;`
- Fixed: Multiple `.Matches()` rules on one property displayed incorrectly — only the first pattern shown, property duplicated (Issue #204)
  - Multiple patterns were placed into separate `allOf` subschemas, which Swagger UI/Redoc/Scalar collapse, keeping only the first `pattern`
  - Now multiple `.Matches()` rules are combined into a single `pattern` via lookahead assertions (e.g. `(?=[\s\S]*(?:[a-z]))(?=[\s\S]*(?:[A-Z]))`), preserving `.Matches()` semantics and rendering correctly
  - Applied to all providers: Swashbuckle, `MicroElements.AspNetCore.OpenApi.FluentValidation`, and NSwag (NSwag previously kept only the last pattern)
  - Changed: `SchemaGenerationOptions.UseAllOfForMultipleRules` default `true` → `false`; set it to `true` to keep the legacy `allOf` representation

# Changes in 7.1.4
- Added: `FluentValidationOperationTransformer` (`IOpenApiOperationTransformer`) for `MicroElements.AspNetCore.OpenApi.FluentValidation` (Issue #200)
  - Query parameters with `[AsParameters]` now receive validation constraints (min/max, required, pattern, etc.)
  - Supports container type resolution with fallback via reflection for `[AsParameters]`
  - Copies validation constraints from schema properties to parameter schemas
  - Registered automatically via `AddFluentValidationRules()`
- Fixed: Nested DTOs in request body not receiving validation constraints (Issue #200)
  - `FluentValidationSchemaTransformer` skipped all property-level schemas, but for nested object types this was the only transformer call
  - Now processes property-level schemas for complex types using the property type's validator

# Changes in 7.1.3
- Fixed: `$ref` replaced with inline schema copy when using `SetValidator` with nested object types (Issue #198)
  - `ResolveRefProperty` (introduced in 7.1.2 for BigInteger isolation) replaced all `$ref` properties with copies, destroying reference structure in the OpenAPI document
  - Fix: snapshot `$ref` properties before rule application, restore them afterwards if no validation constraints were added by rules
  - BigInteger per-model constraints (Issue #146) continue to work correctly

# Changes in 7.1.2
- Added: `BigInteger` support for min/max validation constraints in OpenAPI schema generation (Issue #146)
  - `IsNumeric()` and `NumericToDecimal()` now handle `BigInteger` values
  - `BigInteger` properties with GreaterThan, LessThan, InclusiveBetween, ExclusiveBetween rules produce correct `minimum`/`maximum` in Swagger
  - NSwag provider updated with the same `BigInteger` support
  - Out-of-range `BigInteger` values (exceeding `decimal` range) are handled gracefully via existing try/catch
- Fixed: Shared schema mutation when multiple models reference the same `BigInteger` type with different constraints (net10.0)
  - `ResolveRefProperty` creates an isolated shallow copy before applying rule mutations
  - Prevents `$ref`-based schema corruption across models in `SchemaRepository`
- Fixed: Replaced deprecated `PackageLicenseUrl` with `PackageLicenseExpression` (Issue #144)
- Fixed: Replaced deprecated `PackageIconUrl` with embedded `PackageIcon`

# Changes in 7.1.1
- Fixed: Nested object validation not applied for `[FromQuery]` parameters (Issue #162)
  - When Swashbuckle decomposes `[FromQuery]` models with nested objects into flat parameters (e.g., `operation.op`), the full dot-path name was used for schema property matching instead of the leaf name (`op`)
  - `EqualsIgnoreAll("operation.op", "op")` compared `"OPERATIONOP"` vs `"OP"` and failed to match
  - Strip dot-path prefix using `LastIndexOf('.')` in both `FluentValidationOperationFilter` and `FluentValidationDocumentFilter`
  - Supports arbitrarily deep nesting (e.g., `a.b.c` → `c`)
- Added: `SetNotNullableIfMinimumGreaterThenZero` option to separately control nullable behavior for numeric Minimum constraints (Issue #154, ported from vchirikov fork PR #2)
  - Distinct from existing `SetNotNullableIfMinLengthGreaterThenZero` (for string MinLength)
  - Default: `false` (backward compatible)
- Fixed: `SetNotNullableIfMinLengthGreaterThenZero` option now works in NSwag provider (Issue #154)
  - `NSwagFluentValidationRuleProvider` now accepts `IOptions<SchemaGenerationOptions>`
  - Rules NotEmpty, Length, Comparison, Between respect both nullable options
  - Feature parity across Swashbuckle, AspNetCore.OpenApi, and NSwag providers
- Improved: Comparison/Between rules now use `SetNotNullableIfMinimumGreaterThenZero()` which checks actual Minimum value instead of unconditionally setting not-nullable

# Changes in 7.1.0
- Added: New package `MicroElements.AspNetCore.OpenApi.FluentValidation` for Microsoft.AspNetCore.OpenApi support (Issue #149)
  - Implements `IOpenApiSchemaTransformer` for .NET 9 and .NET 10
  - Supports all FluentValidation rules: Required, NotEmpty, Length, Pattern, Email, Comparison, Between
  - Handles AllOf/OneOf/AnyOf sub-schemas for polymorphic models
  - No dependency on Swashbuckle
  - User-facing API: `services.AddFluentValidationRulesToOpenApi()` + `options.AddFluentValidationRules()`
  - .NET 10: full nested validator support via `GetOrCreateSchemaAsync`
  - .NET 9: limited nested validator support (fallback to empty schema)
- Fixed: AspNetCore.OpenApi.FluentValidation support for .NET 10 (Issue #149, PR #192)
- Added: Sample project `SampleAspNetCoreOpenApi` demonstrating Microsoft.AspNetCore.OpenApi integration
- Added: ADR-001 documenting the architectural decision for AspNetCore.OpenApi support

# Changes in 7.1.0-beta.1
- Added: New package `MicroElements.AspNetCore.OpenApi.FluentValidation` for Microsoft.AspNetCore.OpenApi support (Issue #149)
  - Implements `IOpenApiSchemaTransformer` for .NET 9 and .NET 10
  - Supports all FluentValidation rules: Required, NotEmpty, Length, Pattern, Email, Comparison, Between
  - Handles AllOf/OneOf/AnyOf sub-schemas for polymorphic models
  - No dependency on Swashbuckle
  - User-facing API: `services.AddFluentValidationRulesToOpenApi()` + `options.AddFluentValidationRules()`
  - .NET 10: full nested validator support via `GetOrCreateSchemaAsync`
  - .NET 9: limited nested validator support (fallback to empty schema)
- Added: Sample project `SampleAspNetCoreOpenApi` demonstrating Microsoft.AspNetCore.OpenApi integration
- Added: ADR-001 documenting the architectural decision for AspNetCore.OpenApi support

# Changes in 7.0.6
- Fixed: `[AsParameters]` validation rules not applied on .NET 8 Minimal APIs (Issue #180)
  - On .NET 8, `ModelMetadata.ContainerType` is null for `[AsParameters]` decomposed parameters
  - Added `AsParametersHelper` fallback that resolves the container type via `[AsParameters]` reflection on `MethodInfo`
  - Applied fallback in both `FluentValidationOperationFilter` and `FluentValidationDocumentFilter`
  - Zero regression on .NET 9/10 where `ContainerType` is already populated

# Changes in 7.0.5
- Added: `RemoveUnusedQuerySchemas` option (default: `true`) to control cleanup of
  container type schemas for `[FromQuery]`/`[AsParameters]` types (Issue #180)

# Changes in 7.0.4
- Fixed: `[AsParameters]` types in minimal API and `[FromQuery]` container types create unused schemas in `components/schemas` (Issue #180)
- Added: Support for keyed DI services (Issue #165)
  - Validators registered via `AddKeyedScoped`, `AddKeyedTransient`, `AddKeyedSingleton` are now discovered automatically
- Removed: Deprecated `FluentValidation.AspNetCore` package reference (Issue #164)
  - Replaced with `FluentValidation.DependencyInjectionExtensions` 12.0.0

# Changes in 7.0.3
- Fixed: NullReferenceException when models contain nested object properties (Issue #176 extended)
  - Handle `OpenApiSchemaReference` for nested class properties in `OpenApiRuleContext`
  - Add safe `TryGetValue` check in `NSwagRuleContext`

# Changes in 7.0.2
- Fixed: InvalidCastException when models contain enum properties (Issue #176)
  - In Microsoft.OpenApi 2.x, enum properties are represented as `OpenApiSchemaReference` instead of `OpenApiSchema`
  - Filter out schema references in `GetProperties()` method to avoid cast exception

# Changes in 7.0.1
- Fixed: FluentValidation rules not applied to `[FromForm]` parameters (Issue #170)
  - Added `RequestBody` processing in `FluentValidationOperationFilter` for `multipart/form-data` and `application/x-www-form-urlencoded` content types

# Changes in 6.1.0
- Added support for .NET 8 and .NET 9 to MicroElements.Swashbuckle.FluentValidation.AspNetCore
- Dropped support for .NET 6.0 
- Updated NJsonSchema to version 10.6.10

# Changes in 6.0.0
- see changelog for betas

# Changes in 6.0.0 - beta.3:
- Added: `IFluentValidationRuleProvider` can be replaced with DI
- Added: `ISchemaGenerationOptions.ValidatorSearch`
  - `IsOneValidatorForType`: bool; Value `true`: Gets only one validator (default), `false`: Gets all suitable validators (new)
  - `SearchBaseTypeValidators`: allows to search base type validators
- Fixed: Stack Overflow Exception when using recursive validator type (PR#122 by @rachelpetitto)
- Deleted: `FluentValidationRulesRegistrator`
- Deleted: `SwaggerGenOptions` from filters
- Many minor code cleanups

# Changes in 6.0.0 - beta.2:
- Codebase unified with NSwag
- Added: MicroElements.NSwag.FluentValidation package. Early version
- Change: `INameResolver` removed from FluentValidationRules ctor. Set it from `SchemaGenerationOptions`
- Change: `ISchemaGenerationSettings` merged to `ISchemaGenerationOptions`
- Change: `IValidatorRegistry` and it's implementations moved to MicroElements.OpenApi.FluentValidation namespace and package
- Change: `IValidatorRegistry` can return more than one validator with method `GetValidators`
- Added: `ValidatorSearch` strategy OneForType, ManyForType
- Added: `ISchemaGenerationOptions.ValidatorFilter`, `ISchemaGenerationOptions.RuleFilter`, `ISchemaGenerationOptions.RuleComponentFilter`
  - Default Rule and RuleComponent filters checks that rule or component has no conditions.
  - Default ValidatorFilter checks that validator CanValidateInstancesOfType
- Change: `UseAllOfForMultipleRules` typo fix

# Changes in 6.0.0 - beta.1:
- Abstracted common logic for NSwag
- Moved from `IValidationFactory` (obsolete in FV 11.1.0) to `IValidationRegistry`
- Supported FluentValidation 11 `AddFluentValidationAutoValidation`
- Removed `HttpContextServiceProviderValidatorFactory`
- Experimental `DocumentFilter`

# Changes in 5.7.0:
* Change: ILengthValidator support for arrays. Sets MinItems, MaxItems (PR#108 by biggik)

# Changes in 5.6.0:
* Supported FluentValidation 11

# Changes in 5.5.0:
* Sets min compatibility to Swashbuckle.AspNetCore 6.3.0. (PR#102 by guimabdo)

# Changes in 5.4.0:
* Adding additional fields (Enum, Description) for overridden schema in FluentValidationOperationFilter. (PR#95 by kritsda-jiwatrakan)

# Changes in 5.3.0:
* Fixed Issue #94: Rule with overridden property name unexpectedly applied to property

# Changes in 5.2.0:
* Fixed case with many rules for one property. Issue #92
* Change: NotEmpty rule sets minItems for arrays instead minLength.

# Changes in 5.1.0:
* Use new registration method AddFluentValidationRulesToSwagger instead of AddFluentValidationRules to allow all feature set
* AddFluentValidationRules become obsolete
* Added ability to set ServiceLifetime in AddFluentValidationRulesToSwagger, default value: Scoped. Fixes #83
* Turned off test rule BeforeAll. Fixes #87
* More detailed warnings in FluentValidationRulesScopeAdapter
* Added detailed error on getting absent property by name

# Changes in 5.0.0:
* FluentValidation updated to 10.0.0
* Swashbuckle.AspNetCore updated to 6.0.0
* RuleContext: Obsolete SchemaFilterContext replaced with ReflectionContext (removed dependency on Swashbuckle)
* Dependency Swashbuckle.AspNetCore changed to Swashbuckle.AspNetCore.SwaggerGen which is UI independent (PR#82 by buvinghausen)
* Added INameResolver to resolve names. Issue #80
* Added AddFluentValidationRulesToSwagger extensions to simplify registration
* FluentValidationSwaggerGenOptions renamed to SchemaGenerationOptions, IsAllOffSupported renamed to UseAllOffForMultipleRules 

# Changes in 4.3.0:
* Fixed #79: Adding a simple Length validation to a string field should not make the field non-nullable
* Fixed #76: SetValidator is applying FluentValidation rules to parent object property with same name

# Changes in 4.2.0:
* Swashbuckle.AspNetCore version supports up to 7 (PR#75 by fabich)

# Changes in 4.1.0:
* RuleForEach supported. Issue #66
* SetValidator supported. Issue #68
* Multiple match rules supported with allOf. Issue #69
* Fixed #67: Absence of MinimumLength should not override nullable. (PR#67 by bcronje)
* Fixed #70: Nullability for numerics if MinLength is greater then zero
* Nullable annotations added

# Changes in 4.0.0:
* FluentValidation updated to [9.0.0]
* Swashbuckle.AspNetCore updated to [5.5.1]
* Changed getting included validator (FluentValidation internal API changed)
* New EmailValidator rule compatible with FluentValidation AspNetCoreCompatibleEmailValidator

# Changes in 3.2.0:
* FluentValidation fix version to [8.3.0, 9)
* Swashbuckle.AspNetCore fix version to [5.2.0, 6)
* Base type for numeric switched to decimal to match type change in OpenApi. Fixes floating numbers with nines after period.
* More smart MinLength, MaxLength, Minimum, Maximum that allows to combine rules without override values.
* More strict limits will be used for min and max values that was set more then once in other rules 

# Changes in 3.1.1:
* Mark required properties as not nullable (PR#58 by @manne) Fixes: #55, #57

# Changes in 3.1.0:
* Swashbuckle.AspNetCore updated to version >= 5.2.0
* Fixed: #53 (Missing method exception when using Swashbuckle > 5.0.0)

# Changes in 3.0.0:
* Supports Swashbuckle 5, net core 3 and brand new System.Text.Json
* Swashbuckle.AspNetCore updated to version >= 5.0.0 (new Microsoft.OpenApi)
* FluentValidation updated to version >= 8.3

* FluentValidation property rules of type CollectionValidationRules (RuleForEach()) are no longer exposed #49. 
* New IgnoreAllStringComparer was invented to solve problem with different property name formatting: camelCase, PascalCase, snake_case, kebab-case
* Added NewtonsoftJsonNamingPolicy example to override property name formatting in new System.Text.Json according Newtonsoft.Json.Serialization.NamingStrategy (see: SampleWebApi)
* Fixed invalid documentation on validation rules containing a condition #38
* Fixed: #37 (FluentValidationOperationFilter now uses swachbuckle interface to determine json settings)

# Changes in 3.0.0-rc.6:
* Swashbuckle.AspNetCore updated to version >= 5.0.0

# Changes in 3.0.0-rc.5:
* FluentValidation property rules of type CollectionValidationRules (RuleForEach()) are no longer exposed #49. 

# Changes in 3.0.0-rc.4:
* Swashbuckle.AspNetCore updated to version >= 5.0.0-rc4 (breaking changes: IApiModelResolver was removed from API)
* New IgnoreAllStringComparer was invented to solve problem with different property name formatting: camelCase, PascalCase, snake_case, kebab-case
* Added NewtonsoftJsonNamingPolicy example to override property name formatting in new System.Text.Json according Newtonsoft.Json.Serialization.NamingStrategy (see: SampleWebApi)

# Changes in 3.0.0-rc.3:
* Updated FluentValidation to version >= 8.3
* Fixed invalid documentation on validation rules containing a condition #38

# Changes in 3.0.0-rc.2:
* Swashbuckle.AspNetCore updated to version >= 5.0.0-rc4
* Fixed: #37 (FluentValidationOperationFilter now uses swachbuckle interface to determine json settings)

# Changes in 3.0.0-rc.1:
* Swashbuckle.AspNetCore updated to version >= 5.0.0-rc3 (PR#35 by @vova-lantsov-dev)
* Reintegrated features from 2.2.0

# Changes in 3.0.0-beta.1:
* Swashbuckle.AspNetCore updated to version >= 5.0.0-rc2 (many breaking changes)

# Changes in 3.0.0-alpha.1:
* Swashbuckle.AspNetCore updated to version >= 5.0.0-beta

# Changes in 2.2.0:
* Added HttpContextServiceProviderValidatorFactory to resolve scoped Dependency Injection (PR#34) by @WarpSpideR

# Changes in 2.1.1:
* Fixed MinLength rewrite by MaxLength validator #32

# Changes in 2.1.0:
* Changes: Allow to use SwaggerGenOptions.CustomSchemaIds (PR#31) by @mkjeff

# Changes in 2.0.1:
* Fixed: #24: NullReferenceException on apply rule for operations.
* Changes: Added more debug logging.

# Changes in 2.0.0:
* Swashbuckle.AspNetCore updated and restricted to version [4.0.0, 5.0.0)
* Breaking Changes: FluentValidation updated to 8.1.3 to support when/unless (PR#27) by @emilssonn
* Changes: Running through included validators recursively to add the entire tree (PR#29) by @runebaekkelund
* Changes: Numeric types includes decimal
* Changes: Schema Minimum and Maximum now supports doubles (was only int)
* WARNING: ScopedSwaggerMiddleware doesn't work as expected because Swashbuckle.AspNetCore changed a lot. Looking for workaround.

# Changes in 1.2.0:
* Added: Numeric types includes decimal

# Changes in 1.1.0:
* Swashbuckle.AspNetCore version locked to versions [1.1.0-3.0.0] because version 4.0.0 has breaking changes. Next version will be 2.0.0 according semver.

# Changes in 1.0.0:
* Added ScopedSwaggerMiddleware to resolve error "Cannot resolve 'MyValidator' from root provider because it requires scoped service 'TDependency'"
* Added support for Include
* Bugfixes
* Updated samples and documentation
* Build scripts migrated to MicroElements.Devops
* Build: added SourceLink

# Changes in 0.8.2:
* Fixed: #13: Fixed warning with null schema.Properties

# Changes in 0.8.1:
* Fixed: #12: Fixed NullReferenceException, if schema.Properties is null

# Changes in 0.8.0:
* New feature: FluentValidation rules for get operation parameters binded from models with validators. Adds swagger validation for parameters: Required, MinLength, MaxLength, Minimum, Maximum, Pattern (DataAnnotation works only with [Required]).
* Fixed: #10: Now member search is IgnoreCase
* Fixed: Possible double Required

# Changes in 0.7.0:
* Improved stability and diagnostics
* Added GetValidator error handling, ApplyRule error handling
* Added ability to work without provided FluentValidation (does not break anything)
* Added ability to use Microsoft.Extensions.Logging.Abstractions (no additional dependencies)
* Added logging in error points (logs as warnings)

# Changes in 0.6.0:
* Fixed: #6: Removed empty required array from swagger schema

# Changes in 0.5.0:
* Supported float and double values for IComparisonValidator and IBetweenValidator

# Changes in 0.4.0:
* Refactored to easy add new rules
* Added ability to add rules through DI
Supported validators:
* INotNullValidator (NotNull)
* INotEmptyValidator (NotEmpty)
* ILengthValidator (Length, MinimumLength, MaximumLength, ExactLength)
* IRegularExpressionValidator (Email, Matches)
* IComparisonValidator (GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual)
* IBetweenValidator (InclusiveBetween, ExclusiveBetween)

# Changes in 0.3.0:
* FluentValidationRulesRegistrator moved to main swagger namespace

# Changes in 0.2.0:
* Added FluentValidationRulesRegistrator

# Changes in 0.1.0:
* Added FluentValidationRules.

Full release notes can be found at 
https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/blob/master/CHANGELOG.md
