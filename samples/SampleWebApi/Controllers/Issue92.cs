using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    /// <summary>
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/92
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class Issue92 : Controller
    {
        [HttpPost("[action]")]
        public ActionResult<Bookshelf> CreateBookshelf(CreateBookshelfCommand command)
        {
            return new ObjectResult(new Bookshelf { Books = command.Books })
            {
                StatusCode = 201
            };
        }
    }

    /// <summary>
    /// Bookshelf domain model.
    /// </summary>
    public class Bookshelf
    {
        public string[] Books { get; set; }
    }

    // Command object to send in a POST call.
    public class CreateBookshelfCommand
    {
        public string[] Books { get; set; }
    }

    // Validator to validate that command.
    public class CreateBookshelfCommandValidator : AbstractValidator<CreateBookshelfCommand>
    {
        public CreateBookshelfCommandValidator()
        {
            RuleFor(c => c.Books)
                .NotEmpty();

            RuleForEach(c => c.Books)
                .NotEmpty()
                .MinimumLength(5)
                .MaximumLength(250);
        }
    }
}