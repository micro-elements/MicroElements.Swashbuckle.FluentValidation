using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class Issue54 : Controller
    {
        [HttpGet("[action]")]
        public IActionResult Get(GetRequest query) => Ok();

        public class GetRequest
        {
            [FromQuery]
            public string Id { get; set; }
        }

        public class Validator : AbstractValidator<GetRequest>
        {
            public Validator()
            {
                RuleFor(c => c.Id)
                    .MinimumLength(6);
            }
        }
    }
}