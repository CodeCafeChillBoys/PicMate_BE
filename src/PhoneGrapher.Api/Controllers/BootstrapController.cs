using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/bootstrap")]
public sealed class BootstrapController(IBootstrapService bootstrapService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<BootstrapResponse>> Get(CancellationToken cancellationToken)
    {
        return Ok(await bootstrapService.GetAsync(cancellationToken));
    }
}
