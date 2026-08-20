// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Issue #232: MVC ApiExplorer flattens a controller's [FromForm] complex parameter into one form field per
// property, so Microsoft.AspNetCore.OpenApi emits an inline request-body schema instead of a $ref to the DTO
// component — the schema transformer never sees the DTO. These fixtures need controller endpoints: minimal
// APIs do not flatten form bodies and were never affected.
// NOTE: Program.cs sets SuppressInferBindingSourcesForParameters host-wide, so [FromForm] must be explicit.
using FluentValidation;
using MicroElements.OpenApi.FluentValidation.FileUpload;
using Microsoft.AspNetCore.Mvc;

public class Issue232FormDto
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }
}

public class Issue232FormDtoValidator : AbstractValidator<Issue232FormDto>
{
    public Issue232FormDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(42);
        RuleFor(x => x.Age).GreaterThanOrEqualTo(7).LessThanOrEqualTo(99);
    }
}

[ApiController]
[Route("api/issue232-form")]
public class Issue232ReproController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm] Issue232FormDto dto) => Ok(dto.Name);
}

public class Issue232FileFormDto
{
    public string Title { get; set; } = string.Empty;

    public int Count { get; set; }

    public IFormFile File { get; set; } = default!;
}

public class Issue232FileFormDtoValidator : AbstractValidator<Issue232FileFormDto>
{
    public Issue232FileFormDtoValidator()
    {
        RuleFor(x => x.Title).MaximumLength(20);
        RuleFor(x => x.Count).GreaterThanOrEqualTo(1);

        // Same content-type list as UploadImageRequest on purpose: on net10 all IFormFile parts share a single
        // #/components/schemas/IFormFile component, and FileUploadDescription.Append dedupes identical notes.
        RuleFor(x => x.File).NotNull().FileContentType("image/jpeg", "image/png");
    }
}

// Isolated in the "v2" document: see the note in Program.cs.
[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
[Route("api/issue232-multipart")]
public class Issue232MultipartController : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public IActionResult Post([FromForm] Issue232FileFormDto dto) => Ok(dto.Title);
}

/// <summary>A form DTO with no registered validator: the document must stay untouched.</summary>
public class Issue232NoValidatorFormDto
{
    public string Note { get; set; } = string.Empty;

    public int Size { get; set; }
}

[ApiController]
[Route("api/issue232-novalidator")]
public class Issue232NoValidatorController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm] Issue232NoValidatorFormDto dto) => Ok(dto.Note);
}

public class Issue232PatternFormDto
{
    public string Single { get; set; } = string.Empty;

    public string Triple { get; set; } = string.Empty;
}

public class Issue232PatternFormDtoValidator : AbstractValidator<Issue232PatternFormDto>
{
    public Issue232PatternFormDtoValidator()
    {
        RuleFor(x => x.Single).Matches("^[a-z]+$");
        RuleFor(x => x.Triple).Matches("[a-z]").Matches("[A-Z]").Matches("[0-9]");
    }
}

[ApiController]
[Route("api/issue232-pattern")]
public class Issue232PatternController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm] Issue232PatternFormDto dto) => Ok(dto.Single);
}

public class Issue232ExclusiveFormDto
{
    public int Amount { get; set; }
}

public class Issue232ExclusiveFormDtoValidator : AbstractValidator<Issue232ExclusiveFormDto>
{
    public Issue232ExclusiveFormDtoValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

[ApiController]
[Route("api/issue232-exclusive")]
public class Issue232ExclusiveController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm] Issue232ExclusiveFormDto dto) => Ok(dto.Amount);
}

/// <summary>
/// A nested complex property is flattened to the dotted binding path "Inner.City". The name-insensitive rule
/// matcher skips the separator, so without an explicit guard that key would pick up the rules of the unrelated
/// root property "InnerCity".
/// </summary>
public class Issue232NestedInner
{
    public string City { get; set; } = string.Empty;
}

public class Issue232NestedFormDto
{
    public Issue232NestedInner Inner { get; set; } = new();

    public string InnerCity { get; set; } = string.Empty;
}

public class Issue232NestedFormDtoValidator : AbstractValidator<Issue232NestedFormDto>
{
    // Deliberately nothing on Inner / Inner.City.
    public Issue232NestedFormDtoValidator() => RuleFor(x => x.InnerCity).NotEmpty().MaximumLength(7);
}

[ApiController]
[Route("api/issue232-nested")]
public class Issue232NestedController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm] Issue232NestedFormDto dto) => Ok(dto.InnerCity);
}

/// <summary>An action binding two form parameters gets an <c>allOf</c> of one property bag per parameter.</summary>
public class Issue232MultiAlphaDto
{
    public string Alpha { get; set; } = string.Empty;
}

public class Issue232MultiAlphaDtoValidator : AbstractValidator<Issue232MultiAlphaDto>
{
    public Issue232MultiAlphaDtoValidator() => RuleFor(x => x.Alpha).MaximumLength(3);
}

public class Issue232MultiBetaDto
{
    public string Beta { get; set; } = string.Empty;
}

public class Issue232MultiBetaDtoValidator : AbstractValidator<Issue232MultiBetaDto>
{
    public Issue232MultiBetaDtoValidator() => RuleFor(x => x.Beta).MaximumLength(4);
}

[ApiController]
[Route("api/issue232-multiparam")]
public class Issue232MultiParamController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm] Issue232MultiAlphaDto alpha, [FromForm] Issue232MultiBetaDto beta)
        => Ok(alpha.Alpha + beta.Beta);
}

/// <summary>Rules composed with <c>Include()</c> must reach the form schema like any other rule.</summary>
public class Issue232IncludeFormDto
{
    public string Shared { get; set; } = string.Empty;
}

public class Issue232IncludedRules : AbstractValidator<Issue232IncludeFormDto>
{
    public Issue232IncludedRules() => RuleFor(x => x.Shared).NotEmpty().MaximumLength(11);
}

public class Issue232IncludeFormDtoValidator : AbstractValidator<Issue232IncludeFormDto>
{
    public Issue232IncludeFormDtoValidator() => Include(new Issue232IncludedRules());
}

[ApiController]
[Route("api/issue232-include")]
public class Issue232IncludeController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm] Issue232IncludeFormDto dto) => Ok(dto.Shared);
}

/// <summary>
/// Two form parameters whose DTOs declare a property with the SAME name but different rules. The rule matcher
/// has no type affinity, so each <c>allOf</c> bag must only ever see the validator of the parameter it belongs to.
/// </summary>
public class Issue232CollidingLeftDto
{
    public string Name { get; set; } = string.Empty;
}

public class Issue232CollidingLeftDtoValidator : AbstractValidator<Issue232CollidingLeftDto>
{
    public Issue232CollidingLeftDtoValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(5);
}

public class Issue232CollidingRightDto
{
    public string Name { get; set; } = string.Empty;
}

public class Issue232CollidingRightDtoValidator : AbstractValidator<Issue232CollidingRightDto>
{
    public Issue232CollidingRightDtoValidator() => RuleFor(x => x.Name).MaximumLength(100);
}

[ApiController]
[Route("api/issue232-colliding")]
public class Issue232CollidingController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm] Issue232CollidingLeftDto left, [FromForm] Issue232CollidingRightDto right)
        => Ok(left.Name + right.Name);
}

/// <summary>
/// Three form-bound parameters against two DTO types: more <c>allOf</c> bags than DTOs, so the bags cannot be
/// paired with the parameters by position alone. Isolated in the "v2" document like the other IFormFile fixture.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
[Route("api/issue232-colliding-with-file")]
public class Issue232CollidingWithFileController : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public IActionResult Post(
        [FromForm] Issue232CollidingLeftDto left,
        [FromForm] Issue232CollidingRightDto right,
        IFormFile file) => Ok(left.Name + right.Name + file.Length);
}

// A parameter the action binds directly — a loose scalar, array or IFormFile — is not flattened out of a
// container, but Microsoft.AspNetCore.OpenApi still gives it its own allOf property bag. If such a bag is not
// accounted for, a DTO declared after it claims the foreign bag on a coinciding field name and is then locked
// out of the bag it actually owns. The controllers below place the loose parameter first and in the middle.

public class Issue232LooseFirstDto
{
    public string Name { get; set; } = string.Empty;
}

public class Issue232LooseFirstDtoValidator : AbstractValidator<Issue232LooseFirstDto>
{
    public Issue232LooseFirstDtoValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(5);
}

[ApiController]
[Route("api/issue232-loose-first")]
public class Issue232LooseFirstController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm(Name = "Name")] string loose, [FromForm] Issue232LooseFirstDto dto)
        => Ok(loose + dto.Name);
}

public class Issue232LooseMiddleSubDto
{
    public string Name { get; set; } = string.Empty;
}

public class Issue232LooseMiddleSubDtoValidator : AbstractValidator<Issue232LooseMiddleSubDto>
{
    public Issue232LooseMiddleSubDtoValidator() => RuleFor(x => x.Name).MaximumLength(5);
}

public class Issue232LooseMiddleSuperDto
{
    public string Name { get; set; } = string.Empty;

    public string Age { get; set; } = string.Empty;
}

public class Issue232LooseMiddleSuperDtoValidator : AbstractValidator<Issue232LooseMiddleSuperDto>
{
    public Issue232LooseMiddleSuperDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.Age).MaximumLength(9);
    }
}

[ApiController]
[Route("api/issue232-loose-middle")]
public class Issue232LooseMiddleController : ControllerBase
{
    [HttpPost]
    public IActionResult Post(
        [FromForm] Issue232LooseMiddleSubDto sub,
        [FromForm(Name = "Age")] string loose,
        [FromForm] Issue232LooseMiddleSuperDto super) => Ok(sub.Name + loose + super.Age);
}

/// <summary>
/// The stolen bag can even hold a different JSON type, turning a string-length rule into an array-cardinality
/// one — a document that contradicts the server in both directions.
/// </summary>
public class Issue232LooseArrayDto
{
    public string Tags { get; set; } = string.Empty;
}

public class Issue232LooseArrayDtoValidator : AbstractValidator<Issue232LooseArrayDto>
{
    public Issue232LooseArrayDtoValidator() => RuleFor(x => x.Tags).MaximumLength(7);
}

[ApiController]
[Route("api/issue232-loose-array")]
public class Issue232LooseArrayController : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromForm(Name = "Tags")] string[] tags, [FromForm] Issue232LooseArrayDto dto)
        => Ok(tags.Length + dto.Tags);
}

/// <summary>
/// A file part inside an <c>allOf</c> bag: the Issue #216 <c>encoding.contentType</c> lookup has to walk the
/// bags, not only the body schema's own properties. Isolated in the "v2" document like the other file fixtures.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
[Route("api/issue232-multipart-multiparam")]
public class Issue232MultipartMultiParamController : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public IActionResult Post([FromForm] Issue232FileFormDto file, [FromForm] Issue232MultiAlphaDto alpha)
        => Ok(file.Title + alpha.Alpha);
}
