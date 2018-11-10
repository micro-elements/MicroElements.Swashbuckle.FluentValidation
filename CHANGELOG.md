# Changes in 2.0.0-beta.1:

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
* Fixed: GH-13: Fixed warning with null schema.Properties

# Changes in 0.8.1:
* Fixed: GH-12: Fixed NullReferenceException, if schema.Properties is null

# Changes in 0.8.0:
* New feature: FluentValidation rules for get operation parameters binded from models with validators. Adds swagger validation for parameters: Required, MinLength, MaxLength, Minimum, Maximum, Pattern (DataAnnotation works only with [Required]).
* Fixed: GH-10: Now member search is IgnoreCase
* Fixed: Possible double Required

# Changes in 0.7.0:
* Improved stability and diagnostics
* Added GetValidator error handling, ApplyRule error handling
* Added ability to work without provided FluentValidation (does not break anything)
* Added ability to use Microsoft.Extensions.Logging.Abstractions (no additional dependencies)
* Added logging in error points (logs as warnings)

# Changes in 0.6.0:
* Fixed: GH-6: Removed empty required array from swagger schema

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
