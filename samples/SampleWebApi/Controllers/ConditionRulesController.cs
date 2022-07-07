using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class ConditionRulesController : Controller
    {
        [HttpPost("[action]")]
        public IActionResult AddBlog([FromBody] Club club)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok(club.ClubNumber);
        }
    }

    public class Club
    {
        public string ClubNumber { get; init; } = null!;
        public string? Email { get; init; }
    }

    public class ClubValidator : AbstractValidator<Club>
    {
        public ClubValidator()
        {
            RuleFor(x => x.ClubNumber)
                .Length(4);

            RuleFor(x => x.Email)
                .EmailAddress()
                .MaximumLength(128)
                .NotNull()
                .NotEmpty()
                .When(x => x.ClubNumber == "8088");
        }
    }
}
