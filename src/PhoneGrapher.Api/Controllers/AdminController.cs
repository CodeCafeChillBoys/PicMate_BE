using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(
    IGrapherService grapherService,
    IAdminService adminService) : ControllerBase
{
    [HttpPost("graphers/{grapherProfileId:guid}/kyc")]
    public async Task<IActionResult> ApproveGrapherKyc(
        Guid grapherProfileId,
        [FromQuery] bool approved,
        CancellationToken cancellationToken)
    {
        await grapherService.ApproveKycAsync(grapherProfileId, approved, cancellationToken);
        return NoContent();
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueSummaryResponse>> Revenue(CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetRevenueSummaryAsync(cancellationToken));
    }
}
