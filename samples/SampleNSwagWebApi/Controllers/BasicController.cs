using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using SampleNSwagWebApi.Controllers;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class BasicController : Controller
    {
        [HttpGet("[action]")]
        [ProducesResponseType(typeof(IEnumerable<Customer>), 200)]
        public IActionResult GetWithFluentValidation(BasicGetRequest request)
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
    
    /// <summary>
    /// Sample GET request with headers mapped from nested class.
    /// </summary>
    public class BasicGetRequest
    {
        public StandardHeaders StandardHeaders { get; set; }

        [FromHeader]
        public string ValueFromHeader { get; set; }

        [FromQuery]
        public string ValueFromQuery { get; set; }
    }
    
    /// <summary>
    /// Standard headers.
    /// </summary>
    public class StandardHeaders
    {
        [FromHeader(Name = "TransactionId")]
        public string TransactionId { get; set; }

        [FromHeader(Name = "RequestId")]
        public string RequestId { get; set; }
    }

    [UsedImplicitly]
    public class BasicRequestValidator : AbstractValidator<BasicGetRequest>
    {
        public BasicRequestValidator()
        {
            RuleFor(x => x.StandardHeaders).SetValidator(new StandardHeadersValidator());
            RuleFor(x => x.ValueFromHeader).MaximumLength(10);

            RuleFor(x => x.ValueFromHeader).NotEmpty().WithMessage("Missing value from header");
            RuleFor(x => x.ValueFromQuery).NotEmpty().WithMessage("Missing value from query");
        }
    } 

    public class RequestWithAnnotations
    {
        public HeadersWithAnnotations Headers { get; set; }

        [Required]
        [FromHeader]
        [MaxLength(10)]
        public string ValueFromHeader { get; set; }

        [Required]
        [FromQuery]
        public string ValueFromQuery { get; set; }
    }

    public class HeadersWithAnnotations
    {
        [MinLength(5)]
        [Required]
        [FromHeader(Name = "TransactionId")]
        public string TransactionId { get; set; }

        [RegularExpression(Constants.GuidRegex)]
        [Required]
        [FromHeader(Name = "RequestId")]
        public string RequestId { get; set; }
    }
    
    public class StandardHeadersValidator : AbstractValidator<StandardHeaders>
    {
        public StandardHeadersValidator()
        {
            RuleFor(x => x.TransactionId)
                .NotNull().WithMessage("Missing TransactionId in header")
                .NotEmpty().WithMessage("Value missing for TransactionId")
                .MinimumLength(8);

            RuleFor(x => x.RequestId)
                .NotNull().WithMessage("Missing RequestId in header")
                .NotEmpty().WithMessage("Value missing for RequestId")
                .Matches(Constants.GuidRegex);
        }
    }
}