using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SampleAlternativeNamingStrategy.Validators;

namespace SampleAlternativeNamingStrategy.Contracts
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