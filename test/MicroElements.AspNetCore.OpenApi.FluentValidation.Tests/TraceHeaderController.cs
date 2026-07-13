// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;

// Issue #230 follow-up: header-bound container properties ([FromHeader(Name = ...)]) only surface as
// operation parameters through an MVC controller (with inference suppression, see Program.cs).
[ApiController]
[Route("api/trace")]
public class TraceHeaderController : ControllerBase
{
    [HttpGet]
    public IActionResult Get(TestTraceHeaders headers) => Ok(headers.ToString());
}
