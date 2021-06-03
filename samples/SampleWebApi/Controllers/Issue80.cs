using System;
using System.ComponentModel.DataAnnotations;
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

        //88

        public class BulkSoftwareDeliveryCreateModel
        {
            [Required]
            [MinLength(3)]
            [MaxLength(100)]
            public string Name { get; set; } = null!;

            [MaxLength(255)]
            public string Description { get; set; } = null!;

            [Required]
            [MinLength(3)]
            [MaxLength(100)]
            public string ProductDeliveryGroupCode { get; set; } = null!;

            [Required]
            [RegularExpression(@"^((([0-9]{1,4})\.([0-9]{1,4})\.([0-9]{1,4})))$")]
            public string DisplayVersion { get; set; } = null!;

            [Required]
            public DateTime? ReleaseDate { get; set; } // Must be nullable/required to prevent a default value being used.

            public string? Languages { get; set; }

            public string? Platforms { get; set; }
        }
    }
}