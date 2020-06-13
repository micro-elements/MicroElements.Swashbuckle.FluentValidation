using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SampleWebApi.Contracts;
using SampleWebApi.Validators;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class SampleApiController : Controller
    {
        [HttpGet]
        public IEnumerable<Customer> Get()
        {
            return new[] { new Customer
            {
                Surname = "Bill",
                Forename = "Gates"
            } };
        }

        [HttpPost("[action]")]
        public IActionResult AddCustomer([FromBody] Customer customer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok();
        }

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
        public IActionResult AddObjectA([FromBody] ObjectA objectA)
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

        public class BasicRequestValidator : AbstractValidator<SampleApiController.GetRequest>
        {
            public BasicRequestValidator()
            {
                RuleFor(x => x.Id).NotEmpty().MaximumLength(3);
            }
        }

    }


}