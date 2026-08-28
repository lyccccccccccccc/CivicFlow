using Microsoft.AspNetCore.Mvc;

namespace CivicFlow.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<SystemStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemStatusResponse> GetStatus()
    {
        return Ok(new SystemStatusResponse(
            "CivicFlow.Api",
            "MVP",
            "Healthy",
            DateTimeOffset.UtcNow));
    }
}

public sealed record SystemStatusResponse(
    string Application,
    string Phase,
    string Status,
    DateTimeOffset TimestampUtc);
