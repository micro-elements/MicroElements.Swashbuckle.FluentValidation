using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    /// <summary>
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/103
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class Issue103 : Controller
    {
        [HttpPost("[action]")]
        public IActionResult Add(Dict request) => Ok();
        
        [HttpPost("[action]")]
        public IActionResult Add2(Dict2 request) => Ok();
        
        [HttpPost("[action]")]
        public IActionResult Add3(Dict3 request) => Ok();

        #region Issue: validator for properties from other type

        public class DataType
        {
            public string Value { get; set; }
        }

        public class Dict
        {
            public DataType Type { get; set; }
            public DataType Value { get; set; }
        }
        
        public class DictValidator : AbstractValidator<Dict>
        {
            public DictValidator()
            {
                RuleFor(x => x.Type.Value)
                    .NotEmpty()
                    .NotNull()
                    .MaximumLength(100);

                RuleFor(x => x.Value.Value)
                    .NotEmpty()
                    .NotNull()
                    .MaximumLength(5000);
            }
        }

        #endregion
        
        #region Solution1: Validator per type

        public record DataType100(string Value);
        public record DataType5000(string Value);
        public record Dict2(DataType100 Type, DataType5000 Value);

        public class DataType100Validator : AbstractValidator<DataType100>
        {
            public DataType100Validator()
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .NotNull()
                    .MaximumLength(100);
            }
        }
        
        public class DataType5000Validator : AbstractValidator<DataType5000>
        {
            public DataType5000Validator()
            {
                RuleFor(x => x.Value)
                    .NotEmpty()
                    .NotNull()
                    .MaximumLength(5000);
            }
        }      
        
        public class Dict2Validator : AbstractValidator<Dict2>
        {
            public Dict2Validator()
            {
                RuleFor(x => x.Type).NotNull();
                RuleFor(x => x.Value).NotNull();
            }
        }

        #endregion

        #region Solution2: use strings

        public record Dict3(string Type, string Value);
        public class Dict3Validator : AbstractValidator<Dict3>
        {
            public Dict3Validator()
            {
                RuleFor(x => x.Type)
                    .NotEmpty()
                    .NotNull()
                    .MaximumLength(100);

                RuleFor(x => x.Value)
                    .NotEmpty()
                    .NotNull()
                    .MaximumLength(5000);
            }
        }

        #endregion
    }
}