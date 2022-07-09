using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        public string Id { get; init; } = null!;
        public string ClubNumber { get; init; } = null!;
        public string? Email { get; init; }
    }

    public class ClubValidator : AbstractValidator<Club>
    {


        public ClubValidator()
        {
            RuleFor(x => x.Id)
                .NotNull();

            RuleFor(x => x.ClubNumber)
                .Length(4);

            RuleFor(x => x.Email)
                .EmailAddress()
                .MaximumLength(128)
                .NotNull()
                .NotEmpty()
                .When(x => x.ClubNumber == "8088");

            WhenAsync((_, _) => Task.FromResult(false), () =>
            {
                RuleFor(x => x.ClubNumber)
                    .NotEmpty()
                    .Matches(new Regex(@"^\d$"));
            });
        }
    }
}
