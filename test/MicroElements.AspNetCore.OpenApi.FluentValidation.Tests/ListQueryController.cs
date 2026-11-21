// Copyright (c) MicroElements. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;

// Issue #239: a [FromQuery] collection property is only surfaceable as an array operation parameter
// through the ApiExplorer flattening of an MVC controller parameter (same shape as the issue stand).
[ApiController]
[Route("api/list-query")]
public class ListQueryController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] TestListQueryDto query) => Ok();

    [HttpPost]
    public IActionResult Post([FromBody] TestListQueryDto dto) => Ok();
}

// F1: item-level rules on a [FromQuery] collection (RuleForEach).
[ApiController]
[Route("api/item-rule-query")]
public class ItemRuleQueryController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] TestItemRuleQueryDto query) => Ok();
}

// F2: a DataAnnotations collection bound ([MaxLength]) combined with the #239 validator.
[ApiController]
[Route("api/annotated-list-query")]
public class AnnotatedListQueryController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromQuery] TestAnnotatedListQueryDto query) => Ok();
}