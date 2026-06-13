using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public sealed class DisputesController(IDisputeService disputeService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateDisputeRequest request, CancellationToken cancellationToken)
    {
        var id = await disputeService.CreateDisputeAsync(User.GetUserId(), request, cancellationToken);
        return Ok(new { id });
    }
}
