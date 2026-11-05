using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Common;

/// <summary>
/// Base class for every API controller.
/// Route: /api/v1/{controller name in kebab-case}, e.g. OpenIssuesController -> /api/v1/open-issues
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase;
