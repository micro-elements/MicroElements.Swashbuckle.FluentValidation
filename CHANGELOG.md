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
