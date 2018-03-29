using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SampleWebApi.Contracts;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class CustomersController : Controller
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
    }
}