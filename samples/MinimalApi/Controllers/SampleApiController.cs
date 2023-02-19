using System.ComponentModel.DataAnnotations;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.AspNetCore.Mvc;
// ReSharper disable All
#pragma warning disable CS8618

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class SampleApiController : Controller
    {
        [HttpPost("[action]")]
        public IActionResult AddSample([FromBody] Sample sample)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok();
        }

        [HttpPost("[action]")]
        public IActionResult AddSampleWithDataAnnotations([FromBody] SampleWithDataAnnotations sample)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok();
        }

        [HttpPost("[action]")]
        public IActionResult AddSampleFromQuery([FromQuery] Sample sample)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok();
        }

        [HttpPost("[action]")]
        public IActionResult AddSampleWithNoRequired([FromBody] SampleWithNoRequired customer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok();
        }

        [HttpGet("[action]")]
        public IActionResult Get(GetRequest query) => Ok();

        public class GetRequest
        {
            [FromQuery]
            public string Id { get; set; }
        }

        public class BasicRequestValidator : AbstractValidator<GetRequest>
        {
            public BasicRequestValidator()
            {
                RuleFor(x => x.Id)
                    .NotEmpty()
                    .MaximumLength(3);
            }
        }
    }

    public class Sample
    {
        public string PropertyWithNoRules { get; set; }

        public string NotNull { get; set; }
        public string NotEmpty { get; set; }
        public string EmailAddressRegex { get; set; }
        public string EmailAddress { get; set; }
        public string RegexField { get; set; }

        public int ValueInRange { get; set; }
        public int ValueInRangeExclusive { get; set; }

        public float ValueInRangeFloat { get; set; }
        public double ValueInRangeDouble { get; set; }
        public decimal DecimalValue { get; set; }

        public string NotEmptyWithMaxLength { get; set; }

        // ReSharper disable once InconsistentNaming
        // https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/10
        public string javaStyleProperty { get; set; }
    }

    public class SampleValidator : AbstractValidator<Sample>
    {
        public SampleValidator()
        {
            RuleFor(sample => sample.NotNull).NotNull();
            RuleFor(sample => sample.NotEmpty).NotEmpty();
            RuleFor(sample => sample.EmailAddressRegex).EmailAddress(EmailValidationMode.Net4xRegex);
            RuleFor(sample => sample.EmailAddress).EmailAddress(EmailValidationMode.AspNetCoreCompatible);
            RuleFor(sample => sample.RegexField).Matches(@"(\d{4})-(\d{2})-(\d{2})");

            RuleFor(sample => sample.ValueInRange).GreaterThanOrEqualTo(5).LessThanOrEqualTo(10);
            RuleFor(sample => sample.ValueInRangeExclusive).GreaterThan(5).LessThan(10);

            RuleFor(sample => sample.ValueInRangeFloat).InclusiveBetween(5.1f, 10.2f);
            RuleFor(sample => sample.ValueInRangeDouble).ExclusiveBetween(5.1, 10.2);
            RuleFor(sample => sample.DecimalValue).InclusiveBetween(1.333m, 200.333m);

            RuleFor(sample => sample.javaStyleProperty).MaximumLength(6);

            RuleFor(sample => sample.NotEmptyWithMaxLength).NotEmpty().MaximumLength(50);
        }
    }

    public class SampleWithDataAnnotations
    {
        public string PropertyWithNoRules { get; set; }

        [Required]
        public string NotNull { get; set; }

        [MinLength(1)]
        public string NotEmpty { get; set; }

        [EmailAddress]
        public string EmailAddress { get; set; }

        [RegularExpression(@"(\d{4})-(\d{2})-(\d{2})")]
        public string RegexField { get; set; }

        [Range(5, 10)]
        public int ValueInRange { get; set; }

        [Range(5.1f, 10.2f)]
        public float ValueInRangeFloat { get; set; }

        [Range(5.1, 10.2)]
        public double ValueInRangeDouble { get; set; }

        [Range(1.333, 200.333)]
        public decimal DecimalValue { get; set; }

        [MaxLength(6)]
        public string javaStyleProperty { get; set; }

        [MinLength(1)]
        [MaxLength(50)]
        public string NotEmptyWithMaxLength { get; set; }
    }

    // https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/6
    public class SampleWithNoRequired
    {
        public string PropertyWithNoRules { get; set; }

        public int ValueInRange { get; set; }
    }

    public class SampleWithNoRequiredValidator : AbstractValidator<SampleWithNoRequired>
    {
        public SampleWithNoRequiredValidator()
        {
            RuleFor(sample => sample.ValueInRange).GreaterThanOrEqualTo(5).LessThanOrEqualTo(10);
        }
    }
}