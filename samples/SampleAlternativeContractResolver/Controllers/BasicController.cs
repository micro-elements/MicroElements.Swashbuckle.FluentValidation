using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SampleAlternativeNamingStrategy.Contracts;

namespace SampleAlternativeNamingStrategy.Controllers
{
    [Route("api/[controller]")]
    public class BasicController : Controller
    {
        [HttpGet("[action]")]
        [ProducesResponseType(typeof(IEnumerable<Customer>), 200)]
        public IActionResult GetWithFluentValidation(BasicGetRequest req)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var customers = new[] { new Customer
            {
                Surname = "Bill",
                Forename = "Gates"
            } };

            return Ok(customers);
        }

        [HttpGet("[action]")]
        [ProducesResponseType(typeof(IEnumerable<Customer>), 200)]
        public IActionResult GetWithDataAnnotation(RequestWithAnnotations req)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customers = new[] { new Customer
            {
                Surname = "Bill",
                Forename = "Gates"
            } };

            return Ok(customers);
        }
    }
}