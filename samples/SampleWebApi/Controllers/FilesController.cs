﻿using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    public class FilesController : Controller
    {
        [HttpPost("[action]")]
        public IActionResult UploadFile([FromForm] UploadFileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            return Ok();
        }
    }

    #region Model

    public class UploadFileRequest
    {
        [FromForm(Name = "File")]
        public IFormFile File { get; set; }
    }

    #endregion

    #region Validation

    [UsedImplicitly]
    public class UploadFileRequestValidator : AbstractValidator<UploadFileRequest>
    {
        public UploadFileRequestValidator()
        {
            RuleFor(x => x.File)
                .Cascade(CascadeMode.Stop)
                .NotEmpty();
        }
    }

    #endregion
}
