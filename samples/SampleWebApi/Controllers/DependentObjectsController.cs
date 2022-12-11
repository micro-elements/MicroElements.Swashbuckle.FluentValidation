using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    /// <summary>
    /// https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/5
    /// </summary>
    [Route("api/[controller]")]
    public class DependentObjectsController : Controller
    {
        [HttpPost("[action]")]
        public IActionResult AddObjectA([FromBody] ObjectA objectA)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok();
        }
    }

    #region Model

    public class ObjectA
    {
        public string Key { get; set; }
        public ObjectB ObjectB { get; set; }
    }

    public class ObjectB
    {
        public string Key { get; set; }
        public ObjectC ObjectC { get; set; }
    }

    public class ObjectC
    {
        public string Key { get; set; }
        public ObjectA ObjectA { get; set; }
    }

    #endregion

    #region Validation

    public class ObjectAValidator : AbstractValidator<ObjectA>
    {
        public ObjectAValidator()
        {
            RuleFor(sample => sample.Key).NotNull();
            RuleFor(sample => sample.ObjectB).NotNull();
        }
    }

    public class ObjectBValidator : AbstractValidator<ObjectB>
    {
        public ObjectBValidator()
        {
            RuleFor(sample => sample.Key).NotNull();
            RuleFor(sample => sample.ObjectC).NotNull();
        }
    }

    public class ObjectCValidator : AbstractValidator<ObjectC>
    {
        public ObjectCValidator()
        {
            RuleFor(sample => sample.Key).NotNull();
            RuleFor(sample => sample.ObjectA).NotNull();
        }
    }

    #endregion
}