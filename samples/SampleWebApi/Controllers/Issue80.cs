using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Issue80 : Controller
    {
        [HttpPost("[action]")]
        public IActionResult PostBestShot([FromBody]BestShot body) => Ok();

        [HttpPost("[action]")]
        public IActionResult PostBestShot2([FromQuery] BestShot body) => Ok();

        public class BestShot
        {
            [JsonPropertyName("photo")]
            public string Link { get; set; }

            [JsonPropertyName("zone")]
            public string Area { get; set; }
        }

        public class Validator : AbstractValidator<BestShot>
        {
            public Validator()
            {
                // Rule for 'Link' but in schema should be 'photo'
                RuleFor(bestShot => bestShot.Link)
                    .MinimumLength(5);
            }
        }
    }
}