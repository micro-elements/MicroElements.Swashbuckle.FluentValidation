// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;

// Issue #213: nested [FromQuery] flattening only happens for MVC controllers (minimal APIs do not
// emit dot-path parameters for nested types), so this scenario needs a controller endpoint.
[ApiController]
[Route("api/nested-filter")]
public class NestedFilterController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] TestNestedFilter filter) => Ok(filter.ToString());
}
