using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SampleWebApi.Contracts;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class BasicController : Controller
    {
        [HttpGet("")]
        [ProducesResponseType(typeof(IEnumerable<Customer>), 200)]
        public IActionResult Get(BasicGetRequest req)
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

        [HttpGet("get2")]
        [ProducesResponseType(typeof(IEnumerable<Customer>), 200)]
        public IActionResult Get2(RequestWithAnnotations req)
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