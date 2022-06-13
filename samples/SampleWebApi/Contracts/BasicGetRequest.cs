using System.ComponentModel.DataAnnotations;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using SampleWebApi.Validators;

namespace SampleWebApi.Contracts
{
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
}