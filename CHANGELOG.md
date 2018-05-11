Changes in 0.8.0:
* New feature: FluentValidation rules for operation parameters binded from models with validators. Adds swagger validation for parameters: Required, MinLength, MaxLength, Minimum, Maximum, Pattern (for DataAnnotation only Required works).
* Fixed: Possible double Required

Changes in 0.7.0:
* Improved stability and diagnostics
* Added GetValidator error handling, ApplyRule error handling
* Added ability to work without provided FluentValidation (does not break anything)
* Added ability to use Microsoft.Extensions.Logging.Abstractions (no additional dependencies)
* Added logging in error points (logs as warnings)

Changes in 0.6.0:
* Fixed: GH-6: Removed empty required array from swagger schema

Changes in 0.5.0:
* Supported float and double values for IComparisonValidator and IBetweenValidator

Changes in 0.4.0:
* Refactored to easy add new rules
* Added ability to add rules through DI
Supported validators:
* INotNullValidator (NotNull)
* INotEmptyValidator (NotEmpty)
* ILengthValidator (Length, MinimumLength, MaximumLength, ExactLength)
* IRegularExpressionValidator (Email, Matches)
* IComparisonValidator (GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual)
* IBetweenValidator (InclusiveBetween, ExclusiveBetween)

Changes in 0.3.0:
* FluentValidationRulesRegistrator moved to main swagger namespace

Changes in 0.2.0:
* Added FluentValidationRulesRegistrator

Changes in 0.1.0:
* Added FluentValidationRules.

Full release notes can be found at 
https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/blob/master/CHANGELOG.md
