using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using MicroElements.OpenApi.FluentValidation;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class ConditionRulesController : Controller
    {
        [HttpPost("[action]")]
        public IActionResult AddClub([FromBody] Club club)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok(club.ClubNumber);
        }
    }

    public interface IIdOwner
    {
        public string Id { get; init; }
    }

    public interface IEmailOwner
    {
        public string? Email { get; init; }
    }

    public class Club : IEmailOwner, IIdOwner
    {
        public string Id { get; init; } = null!;
        public string ClubNumber { get; init; } = null!;
        public string? Email { get; init; }
        public int? Source { get; init; }
    }

    public class ClubValidator : AbstractValidator<Club>
    {
        public ClubValidator()
        {
            this.IncludeConditionalRulesInSchema();

            RuleFor(x => x.Id)
                .NotNull();

            RuleFor(x => x.ClubNumber)
                .Length(4);

            RuleFor(x => x)
                .SetValidator(new IdOwnerValidator());

            RuleFor(x => x)
                .SetValidator(new EmailOwnerValidator())
                .When(x => x.ClubNumber == "8088");

            When((_, _) => false, () =>
            {
                RuleFor(x => x.ClubNumber)
                    .NotEmpty()
                    .Matches(new Regex(@"^\d$"))
                    .When(x => x.Id == "1");

                RuleFor(x => x.Source)
                    .LessThanOrEqualTo(16)
                    .When(x => x is not null)
                    .ExcludeConditionalRuleFromSchema();
            });
        }
    }

    public class IdOwnerValidator : AbstractValidator<IIdOwner>
    {
        public IdOwnerValidator()
        {
            this.IncludeConditionalRulesInSchema();

            RuleFor(x => x.Id)
                .MaximumLength(7)
                .When(x => true);

            RuleFor(x => x.Id)
                .MinimumLength(2)
                .When(x => false)
                .ExcludeConditionalRuleFromSchema();
        }
    }

    public class EmailOwnerValidator : AbstractValidator<IEmailOwner>
    {
        public EmailOwnerValidator()
        {
            RuleFor(x => x.Email)
                .EmailAddress()
                .MaximumLength(128)
                .NotNull()
                .NotEmpty()
                .When(x => true)
                .IncludeConditionalRuleInSchema();
        }
    }
}